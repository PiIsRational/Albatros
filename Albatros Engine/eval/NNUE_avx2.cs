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
    long netOutput = 0;

    //feature transformer
    readonly FeatureTransformer transformer = new(768, TRANSFORMER_OUT_SIZE);
    public const int TRANSFORMER_OUT_SIZE = 256;

    //All the other Layers
    readonly LinearLayer output = new(2 * TRANSFORMER_OUT_SIZE, 1);

    //other stuff
    readonly Vector256<int> kOnes256 = new();
    readonly short[] initKOnes256 = new short[TRANSFORMER_OUT_SIZE];

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
    readonly standart stuff = new();

    const int REGISTER_WIDTH = 8;
    //Size of the Feature Transformer Output size (16) divided by the register width (8)
    const int NUMBER_OF_CHUNKS = TRANSFORMER_OUT_SIZE / REGISTER_WIDTH;
    readonly Vector256<int>[] registers = new Vector256<int>[NUMBER_OF_CHUNKS];

    public NNUE_avx2(bool LoadNet)
    {
        InitTransformer();
        InitOutput();

        //initkOnes256();
        initPtype();

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

    public void initPtype()
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

    public void updateAccFromMove(Position board, ReverseMove move, bool invert)
    {
        List<int>[] featuresToAdd = new List<int>[2];
        List<int>[] featuresToRemove = new List<int>[2];

        byte removed_piece_color = (byte)(move.removed_piece_idx != 0 ? (move.removed_pieces[0, 1] >> 3) : board.color);

        for (int color = 0; color < 2; color++)
        {
            featuresToAdd[color] = new List<int>();
            featuresToRemove[color] = new List<int>();

            for (int i = 0; i < move.removed_piece_idx; i++)
                featuresToRemove[color].Add((pieceEncoding[color][move.removed_pieces[i, 1]] + move.removed_pieces[i, 0]) ^ (color == 1 ? 0 : 56));

            for (int i = 0; i < move.moved_piece_idx; i++)
            {
                featuresToAdd[color].Add((pieceEncoding[color][board.board[move.moved_pieces[i, 1]]] + move.moved_pieces[i, 1]) ^ (color == 1 ? 0 : 56));
                if (removed_piece_color == board.color) featuresToRemove[color].Add((pieceEncoding[color][board.board[move.moved_pieces[i, 1]]] + move.moved_pieces[i, 0]) ^ (color == 1 ? 0 : 56));
            }

            acc = invert
                ? UpdateAcc(transformer, acc, featuresToRemove, featuresToAdd, (byte)color)
                : UpdateAcc(transformer, acc, featuresToAdd, featuresToRemove, (byte)color);
        }
    }
    //calculate accumulator values from start
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
            fixed (int* currentAddress = &acc.Acc[color][i * REGISTER_WIDTH])
            {
                //store the register ath this address
                Avx2.Store(currentAddress, registers[i]);
            }
        }
        return acc;
    }
    //just update the accumulator values
    unsafe public Accumulator UpdateAcc(FeatureTransformer transformer, Accumulator acc, List<int>[] addedFeatures, List<int>[] removedFeatures, byte color)
    {
        //Load the old accumulator into the registers
        for (int i = 0; i < NUMBER_OF_CHUNKS; i++)
        {
            //get the address of the accumulator
            fixed (int* currentAddress = &acc.Acc[color][i * REGISTER_WIDTH])
            {
                //load this part of the register with the data of the address
                registers[i] = Avx2.LoadVector256(currentAddress);
            }
        }

        //Remove the old features 
        foreach (int Place in removedFeatures[color])
        {
            for (int i = 0; i < NUMBER_OF_CHUNKS; i++)
            {
                //get the address of the weights
                fixed (int* currentAddress = &transformer.weight[Place, i * REGISTER_WIDTH])
                {
                    //add the weights withe the register
                    registers[i] = Avx2.Subtract(registers[i], Avx2.LoadVector256(currentAddress));
                }
            }
        }

        //Add the new features
        foreach (int Place in addedFeatures[color])
        {
            for (int i = 0; i < NUMBER_OF_CHUNKS; i++)
            {
                //get the address of the weights
                fixed (int* currentAddress = &transformer.weight[Place, i * REGISTER_WIDTH])
                {
                    //add the weights withe the register
                    registers[i] = Avx2.Add(registers[i], Avx2.LoadVector256(currentAddress));
                }
            }
        }

        //store the registers into the accumulator
        for (int i = 0; i < NUMBER_OF_CHUNKS; i++)
        {
            //get the address of the accumulator
            fixed (int* currentAddress = &acc.Acc[color][i * REGISTER_WIDTH])
            {
                //store the register ath this address
                Avx2.Store(currentAddress, registers[i]);
            }
        }

        return acc;
    }

    //do the Matrix multplication for the linear layers
    /*
    unsafe int[] LinearLayer(LinearLayer layer, short[] Input, int[] Output)
    {
        //One register size is 256 bits it isfilled up with signed int16 s so the size is 256 / 16 = 16  
        const int register_width = 16;
        int num_in_chunks = layer.Input_size / register_width;
        int num_out_chunks = layer.Output_size / 4;


        for (int i = 0; i < num_out_chunks; i++)
        {
            //initialize the weight offsets each offset corresponds to one row of weights
            int offset0 = (i * 4 + 0) * layer.Input_size;
            int offset1 = (i * 4 + 1) * layer.Input_size;
            int offset2 = (i * 4 + 2) * layer.Input_size;
            int offset3 = (i * 4 + 3) * layer.Input_size;

            //initialize the sum vectors each vector will hold one row of the weights matrix
            Vector256<int> sum0 = new Vector256<int>();
            Vector256<int> sum1 = new Vector256<int>();
            Vector256<int> sum2 = new Vector256<int>();
            Vector256<int> sum3 = new Vector256<int>();

            //at each pass the loop processes a 32*4 chunk of weights
            for (int j = 0; j < num_in_chunks; j++)
            {
                Vector256<byte> In;
                fixed (byte* currentAddress = &Input[j * register_width]) 
                {
                    In = Avx2.LoadVector256(currentAddress);
                }

                fixed (short* currentAdress = &layer.weight[offset0 + j * register_width])
                {
                    sum0 = m256_add_dpbusd_epi32(sum0, In, Avx2.LoadVector256(currentAdress));
                }
                fixed (short* currentAdress = &layer.weight[offset1 + j * register_width])
                {
                    sum1 = m256_add_dpbusd_epi32(sum1, In, Avx2.LoadVector256(currentAdress));
                }
                fixed (short* currentAdress = &layer.weight[offset2 + j * register_width])
                {
                    sum2 = m256_add_dpbusd_epi32(sum2, In, Avx2.LoadVector256(currentAdress));
                }
                fixed (short* currentAdress = &layer.weight[offset3 + j * register_width])
                {
                    sum3 = m256_add_dpbusd_epi32(sum3, In, Avx2.LoadVector256(currentAdress));
                }
            }

            Vector128<int> Bias;
            fixed(int* currentAdress = &layer.bias[i * 4])
            {
                Bias = Avx2.LoadVector128(currentAdress);
            }

            Vector128<int> outval = m256_haddx4(sum0, sum1, sum2, sum3, Bias);

            outval = Avx2.ShiftRightArithmetic(outval, Log2WeightScale);

            fixed (int* currentAddress = &Output[i * 4])
            {
                Avx2.Store(currentAddress, outval);
            }
        }
        return Output;

    }

    public Vector128<int> m256_haddx4(Vector256<int> sum0 , Vector256<int>sum1 , Vector256<int>sum2 , Vector256<int> sum3, Vector128<int> Bias)
    {
        sum0 = Avx2.HorizontalAdd(sum0, sum1);
        sum2 = Avx2.HorizontalAdd(sum2, sum3);

        sum0 = Avx2.HorizontalAdd(sum0, sum2);

        Vector128<int> sum128lo = Avx2.ExtractVector128(sum0, 0);
        Vector128<int> sum128hi = Avx2.ExtractVector128(sum0, 1);

        return Avx2.Add(Avx2.Add(sum128lo, sum128hi), Bias);
    }

    unsafe public void initkOnes256()
    {
        for (int i = 0; i < 16; i++)
            initKOnes256[i] = 1;

        fixed (short* currentadress = &initKOnes256[0])
        {
            kOnes256 = Avx2.LoadVector256(currentadress);
        }
    }
    public Vector256<int> m256_add_dpbusd_epi32(Vector256<int> source , Vector256<short> a , Vector256<short> b)
    {

        // Multiply a * b and accumulate neighbouring outputs into int16 values
        Vector256<int> product = Avx2.MultiplyAddAdjacent(a, b);

        // Multiply product0 by 1 (idempotent) and accumulate neighbouring outputs into int32 values
        Vector256<int> product0 = Avx2.MultiplyAddAdjacent(product, kOnes256);

        // Add to the main int32 accumulator.
        return Avx2.Add(source, product0);
    }
    */
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

    //clipped relu for the accumulator output
    unsafe public byte[] crelu16(int size, byte[] Output, short[] InputA, short[] InputB)
    {
        const int in_register_width = 256 / 16;
        const int out_register_width = 256 / 8;
        int num_out_chunks = size / (2 * out_register_width);

        Vector256<sbyte> zero = new();
        byte control = 0b11011000;
        //control = 0b00100111;
        for (int i = 0; i < num_out_chunks; i++)
        {
            //calucalte for InputA
            Vector256<short> in0, in1;
            fixed (short* currentPointer0 = &InputA[in_register_width * ((i * 2) + 0)], currentPointer1 = &InputA[in_register_width * ((i * 2) + 1)])
            {
                in0 = Avx2.ShiftRightArithmetic(Avx2.LoadVector256(currentPointer0), log2WeightScale);
                in1 = Avx2.ShiftRightArithmetic(Avx2.LoadVector256(currentPointer1), log2WeightScale);
            }

            Vector256<byte> resultA = Avx2.Permute4x64(Avx2.Max(Avx2.PackSignedSaturate(in0, in1), zero).AsInt64(), control).AsByte();

            //calculate for InputB
            Vector256<short> in2, in3;
            fixed (short* currentPointer2 = &InputB[in_register_width * ((i * 2) + 0)], currentPointer3 = &InputB[in_register_width * ((i * 2) + 1)])
            {
                in2 = Avx2.ShiftRightArithmetic(Avx2.LoadVector256(currentPointer2), log2WeightScale);
                in3 = Avx2.ShiftRightArithmetic(Avx2.LoadVector256(currentPointer3), log2WeightScale);
            }

            Vector256<byte> resultB = Avx2.Permute4x64(Avx2.Max(Avx2.PackSignedSaturate(in2, in3), zero).AsInt64(), control).AsByte();

            fixed (byte* currentPointerA = &Output[i * out_register_width], currentPointerB = &Output[(i * out_register_width) + (size / 2)])
            {
                Avx2.Store(currentPointerA, resultA);
                Avx2.Store(currentPointerB, resultB);
            }
        }

        return Output;
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
    public int[][] Acc;

    public Accumulator(int Size)
    {
        Acc = new int[2][];
        Acc[0] = new int[Size];
        Acc[1] = new int[Size];
    }

    public int[] ReturnSide(byte color)
    {
        return Acc[color];
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