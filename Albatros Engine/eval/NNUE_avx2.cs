using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

class NNUE_avx2
{
    //input into the feature transformer
    public List<int>[] features = new List<int>[2];

    //output of the Feature transformers
    public Accumulator acc = new(TRANSFORMER_OUT_SIZE);

    //acc gone trough the relu layer
    public short[] accReluOut = new short[2 * TRANSFORMER_OUT_SIZE];

    //Network Output
    long netOutput;

    //feature transformer
    readonly FeatureTransformer transformer = new(768, TRANSFORMER_OUT_SIZE);
    public const int TRANSFORMER_OUT_SIZE = 256;

    //All the other Layers
    readonly LinearLayer output = new(2 * TRANSFORMER_OUT_SIZE, 1);

    // weights are between 2 and -2 (2^9 and -2^9)
    const int WEIGHT_SCALING = 1024;
    const int MAX_WEIGHT_SIZE = 2048;

    // activations between 4 and -4 (2^15 and -2^15)
    const int ACTIVATION_SCALING = 8192;

    //returns the piecetype of the halfkp encoding
    //piecetype[0] is for black and piecetype[1] is for white
    private static readonly int[][] pieceEncoding = new int[2][];

    //Log2 of weigtscale
    const byte log2WeightScale = 10;

    const int REGISTER_WIDTH = 8;
    //Size of the Feature Transformer Output size (16) divided by the register width (8)
    const int NUMBER_OF_CHUNKS = TRANSFORMER_OUT_SIZE / REGISTER_WIDTH;
    readonly Vector256<int>[] registers = new Vector256<int>[NUMBER_OF_CHUNKS];

    public NNUE_avx2(bool LoadNet)
    {
        InitTransformer();
        InitOutput();

        //initkOnes256();
        InitPtype();

        //initialize the feature vectors
        features[0] = new List<int>();
        features[1] = new List<int>();

        if (LoadNet && File.Exists("ValueNet.nnue"))
            LoadNetFile("ValueNet.nnue");

        if (LoadNet && File.Exists("floats net.nnue"))
            LoadOldNetFile("floats net.nnue");
    }

    public void set_acc_from_position(Position inputBoard)
    {
        //load the features for the board into the feature vector
        features = BoardToHalfP(inputBoard);

        //load the feature vector into the accumulator
        acc = RefreshAcc(transformer, acc, features, 0);
        acc = RefreshAcc(transformer, acc, features, 1);
    }

    //initialize the four Layers
    public void InitTransformer()
    {
        //Weights
        for (int i = 0; i < transformer.weight.GetLength(0); i++)
        {
            for (int j = 0; j < transformer.weight.GetLength(1); j++)
                transformer.weight[i, j] = 1;
        }
        //Biases
        for (int i = 0; i < transformer.bias.Length; i++)
            transformer.bias[i] = 1;
    }

    public void InitOutput()
    {
        for (int i = 0; i < output.Input_size; i++)
            output.weight[i] = 1;

        output.bias[0] = 1;
    }

    //inittialize the piecetype array
    public List<int>[] BoardToHalfP(Position board)
    {
        List<int>[] Features = new List<int>[2];
        for (int color = 0; color <= 1; color++)
        {
            Features[color] = new List<int>();

            for (int square = 0; square < 64; square++)
            {
                if (pieceEncoding[color][board.board[square]] > -1)
                    Features[color].Add((pieceEncoding[color][board.board[square]] + square) ^ (color == 1 ? 0 : 56));
            }
        }

        return Features;
    }

    public void InitPtype()
    {
        for (byte color = 0; color < 2; color++)
        {
            pieceEncoding[color] = new int[15];
            for (int piece = 0; piece < 15; piece++)
            {
                if (piece == 0 || piece == 7 || piece == 8)
                    pieceEncoding[color][piece] = -1;
                else
                {
                    int pieceType = piece - (piece < 7 ? 1 : 3);
                    pieceEncoding[color][piece] = ChangeType(pieceType, color);
                }
            }
        }
    }

    public int ChangeType(int piecetype, byte color)
    {
        return color == 0 ? piecetype * 64 : (piecetype + (piecetype > 5 ? -6 : 6)) * 64;
    }

    //takes in the accumulator and gives back the position value
    public int AccToOutput(Accumulator acc, byte color)
    {
        //add clipped relu to accumulator
        accReluOut = crelu32(accReluOut, acc.ReturnSide(color), acc.ReturnSide((byte)(1 - color)));

        //perform matrix multiplication with Output layer
        netOutput = CalculateOutputValue(output, accReluOut);

        //return the Netoutput scaled back and as an integer
        return (int)(netOutput * 1000 / (WEIGHT_SCALING * ACTIVATION_SCALING));
    }

    public void UpdateAccFromMove(Position board, ReverseMove move, bool invert)
    {
        List<int> featuresToAdd = new List<int>();
        List<int> featuresToRemove = new List<int>();

        byte removed_piece_color = (byte)(move.removedPieceIdx != 0 ? (move.removedPieces[0, 1] >> 3) : board.color);

        for (int color = 0; color < 2; color++)
        {
            featuresToAdd.Clear();
            featuresToRemove.Clear();

            for (int i = 0; i < move.removedPieceIdx; i++)
                featuresToRemove.Add(pieceEncoding[color][move.removedPieces[i, 1]] + move.removedPieces[i, 0] ^ (color == 1 ? 0 : 56));

            for (int i = 0; i < move.movedPieceIdx; i++)
            {
                featuresToAdd.Add(pieceEncoding[color][board.board[move.movedPieces[i, 1]]] + move.movedPieces[i, 1] ^ (color == 1 ? 0 : 56));
                if (removed_piece_color == board.color) featuresToRemove.Add(pieceEncoding[color][board.board[move.movedPieces[i, 1]]] + move.movedPieces[i, 0] ^ (color == 1 ? 0 : 56));
            }

            acc = invert
                ? UpdateAcc(transformer, acc, featuresToRemove, featuresToAdd, (byte)color)
                : UpdateAcc(transformer, acc, featuresToAdd, featuresToRemove, (byte)color);
        }
    }

    unsafe public Accumulator RefreshAcc(FeatureTransformer transformer, Accumulator acc, List<int>[] features, byte color)
    {
        //Size of the Feature Transformer Output size divided by the register width
        const int CHUNK_COUNT = TRANSFORMER_OUT_SIZE / REGISTER_WIDTH;
        //Generate the avx2 registers
        Vector256<int>[] registers = new Vector256<int>[CHUNK_COUNT];

        //Load the bias into the registers
        for (int i = 0; i < CHUNK_COUNT; i++)
        {
            //get the address of the bias
            fixed (int* currentAddress = &transformer.bias[i * REGISTER_WIDTH])
            {
                //load this part of the register with the data of the address
                registers[i] = Avx2.LoadVector256(currentAddress);
            }
        }

        //Add the weights
        foreach (int place in features[color])
        {
            for (int i = 0; i < CHUNK_COUNT; i++)
            {
                //get the address of the weights
                fixed (int* currentAddress = &transformer.weight[place, i * REGISTER_WIDTH])
                {
                    //add the weights withe the register
                    registers[i] = Avx2.Add(registers[i], Avx2.LoadVector256(currentAddress));
                }
            }
        }

        //store the registers into the accumulator
        for (int i = 0; i < CHUNK_COUNT; i++)
        {
            //get the address of the accumulator
            fixed (int* currentAddress = &acc.accu[color][i * REGISTER_WIDTH])
            {
                //store the register ath this address
                Avx2.Store(currentAddress, registers[i]);
            }
        }
        return acc;
    }
    //just update the accumulator values
    unsafe public Accumulator UpdateAcc(FeatureTransformer transformer, Accumulator acc, List<int> addedFeatures, List<int> removedFeatures, byte color)
    {
        fixed (int* currentAddress = &acc.accu[color][0])
        fixed (Vector256<int>* regAdd = &registers[0])
        {
            for (int i = 0; i < NUMBER_OF_CHUNKS; i++)
               *(regAdd + i) = Avx2.LoadVector256(currentAddress + i * REGISTER_WIDTH);

            foreach (int place in removedFeatures)
            {
                fixed (int* tadd = &transformer.weight[place, 0])
                {
                    for (int i = 0; i < NUMBER_OF_CHUNKS; i++)
                        *(regAdd + i) = Avx2.Subtract(*(regAdd + i), Avx2.LoadVector256(tadd + i * REGISTER_WIDTH));
                }
            }

            foreach (int place in addedFeatures)
            {
                fixed (int* tadd = &transformer.weight[place, 0])
                {
                    for (int i = 0; i < NUMBER_OF_CHUNKS; i++)
                        *(regAdd + i) = Avx2.Add(*(regAdd + i), Avx2.LoadVector256(tadd + i * REGISTER_WIDTH));
                }
            }

            for (int i = 0; i < NUMBER_OF_CHUNKS; i ++)
                Avx2.Store(currentAddress + i * REGISTER_WIDTH, *(regAdd + i));     
        }
        
        return acc;
    }

    unsafe long CalculateOutputValue(LinearLayer layer, short[] input)
    {
        const int register_width = 16;
        int num_in_chunks = input.Length / register_width;
        Vector256<short> In;
        Vector256<short> layer_matrix;
        Vector256<int> accumulator = new();
        netOutput = 0;

        int[] intermediateArray = new int[register_width / 2];

        for (int i = 0; i < num_in_chunks; i++)
        {
            fixed (short* currentAddress = &input[register_width * i])
                In = Avx2.LoadVector256(currentAddress);

            fixed (short* currentAddress = &layer.weight[register_width * i])
                layer_matrix = Avx2.LoadVector256(currentAddress);

            accumulator = Avx2.Add(accumulator, Avx2.MultiplyAddAdjacent(In, layer_matrix));
        }

        fixed (int* currentAddress = &intermediateArray[0])
            Avx2.Store(currentAddress, accumulator);

        foreach (int value in intermediateArray)
            netOutput += value;

        return netOutput + layer.bias[0];
    }

    /// <summary>
    /// clipped relu for the linear layer Outputs 
    /// clamps the input values between 0 and the maximal activation size (4)
    /// </summary>
    /// <param name="Output">the vector to output</param>
    /// <param name="InputA">the first input</param>
    /// <param name="InputB">the second input</param>
    /// <returns>the output vector containing the right values</returns>
    unsafe public short[] crelu32(short[] Output, int[] InputA, int[] InputB)
    {
        const int in_register_width = 256 / 32;
        const int out_register_width = 256 / 16;
        int num_out_chunks = Output.Length / (2 * out_register_width);
        byte control = 0b11011000;

        Vector256<short> zero = new();

        for (int i = 0; i < num_out_chunks; i++)
        {
            Vector256<short> in0;
            fixed (int* PointerA = &InputA[in_register_width * ((i * 2) + 0)], PointerB = &InputA[in_register_width * ((i * 2) + 1)])
                in0 = Avx2.PackSignedSaturate(Avx2.ShiftRightArithmetic(Avx2.LoadVector256(PointerA), log2WeightScale), Avx2.ShiftRightArithmetic(Avx2.LoadVector256(PointerB), log2WeightScale));

            Vector256<short> resultA = Avx2.Permute4x64(Avx2.Max(in0, zero).AsInt64(), control).AsInt16();

            Vector256<short> in1;
            fixed (int* PointerA = &InputB[in_register_width * ((i * 2) + 0)], PointerB = &InputB[in_register_width * ((i * 2) + 1)])
                in1 = Avx2.PackSignedSaturate(Avx2.ShiftRightArithmetic(Avx2.LoadVector256(PointerA), log2WeightScale), Avx2.ShiftRightArithmetic(Avx2.LoadVector256(PointerB), log2WeightScale));

            Vector256<short> resultB = Avx2.Permute4x64(Avx2.Max(in1, zero).AsInt64(), control).AsInt16();

            fixed (short* PointerA = &Output[i * out_register_width], PointerB = &Output[(i * out_register_width) + (Output.Length / 2)])
            {
                Avx2.Store(PointerA, resultA);
                Avx2.Store(PointerB, resultB);
            }
        }

        return Output;
    }

    public void LoadOldNetFile(string filename)
    {
        NumberFormatInfo nfi = new();
        nfi.NumberDecimalSeparator = ".";

        StreamReader sr = new(filename);
        string[] floats = sr.ReadToEnd().Split(' ');
        int index = 0;

        for (int i = 0; i < output.weight.Length; i++)
        {
            output.weight[i] = MaxMinWeight(Convert.ToSingle(floats[index], nfi) * WEIGHT_SCALING);
            index++;
        }

        for (int i = 0; i < output.bias.Length; i++)
        {
            output.bias[i] = MaxMinWeight(Convert.ToSingle(floats[index], nfi) * WEIGHT_SCALING) * ACTIVATION_SCALING;
            index++;
        }

        for (int i = 0; i < transformer.weight.GetLength(1); i++)
        {
            for (int j = 0; j < transformer.weight.GetLength(0); j++)
            {
                transformer.weight[j, i] = MaxMinWeight(Convert.ToSingle(floats[index], nfi) * WEIGHT_SCALING) * ACTIVATION_SCALING;
                index++;
            }
        }

        for (int i = 0; i < transformer.bias.Length; i++)
        {
            transformer.bias[i] = MaxMinWeight(Convert.ToSingle(floats[index], nfi) * WEIGHT_SCALING) * ACTIVATION_SCALING;
            index++;
        }

        sr.Close();
    }

    public void LoadNetFile(string filename)
    {
        StreamReader sr = new(filename);

        //Weights

        //Output
        load_layer_weights(output, sr);

        //Biases

        //Output
        load_layer_bias(output, sr);

        //Transformer
        for (int i = 0; i < transformer.weight.GetLength(0); i++)
        {
            for (int j = 0; j < transformer.weight.GetLength(1); j++)
            {
                byte[] arr = new byte[4];
                for (int k = 0; k < arr.Length; k++)
                    arr[k] = (byte)sr.Read();

                transformer.weight[i, j] = BitConverter.ToInt32(arr);
            }
        }

        //Transformer bias
        for (int i = 0; i < transformer.bias.Length; i++)
        {
            byte[] arr = new byte[4];
            for (int k = 0; k < arr.Length; k++)
                arr[k] = (byte)sr.Read();

            transformer.bias[i] = BitConverter.ToInt32(arr);
        }

        sr.Close();
    }
    public void load_layer_weights(LinearLayer layer, StreamReader sr)
    {
        for (int i = 0; i < layer.weight.Length; i++)
        {
            byte[] arr = new byte[2];
            for (int j = 0; j < arr.Length; j++)
                arr[j] = (byte)sr.Read();

            layer.weight[i] = BitConverter.ToInt16(arr);
        }
    }
    public void load_layer_bias(LinearLayer layer, StreamReader sr)
    {
        for (int i = 0; i < layer.bias.Length; i++)
        {
            byte[] arr = new byte[4];
            for (int j = 0; j < arr.Length; j++)
                arr[j] = (byte)sr.Read();

            layer.bias[i] = BitConverter.ToInt32(arr);
        }
    }

    public short MaxMinWeight(float input)
    {
        if (input >= MAX_WEIGHT_SIZE)
            return MAX_WEIGHT_SIZE;
        return input <= -MAX_WEIGHT_SIZE ? (short)-MAX_WEIGHT_SIZE : Convert.ToInt16(input);
    }
}
class Accumulator
{
    //Accumulator (Acc[1] White, Acc[0] Black)
    public int[][] accu;

    public Accumulator(int Size)
    {
        accu = new int[2][];
        accu[0] = new int[Size];
        accu[1] = new int[Size];
    }

    public int[] ReturnSide(byte color)
    {
        return accu[color];
    }
}
class LinearLayer
{
    public short[] weight;
    public int[] bias;
    public int Input_size = 0, Output_size = 0;
    public LinearLayer(int ColumnSize, int RowSize)
    {
        Input_size = ColumnSize;
        Output_size = RowSize;
        weight = new short[ColumnSize * RowSize];
        bias = new int[RowSize];
    }
}
class FeatureTransformer
{
    public int[,] weight;
    public int[] bias;
    public FeatureTransformer(int ColumnSize, int RowSize)
    {
        weight = new int[ColumnSize, RowSize];
        bias = new int[RowSize];
    }
}