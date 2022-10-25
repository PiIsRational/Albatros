using System;
using System.Collections.Generic;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.IO;
using System.Diagnostics;

class NNUE_avx2
{
    //input into the feature transformer
    public List<int>[] Features = new List<int>[2];
    //output of the Feature transformers
    public Accumulator acc = new Accumulator(16);
    //acc gone trough the relu layer
    public short[] accReluOut = new short[32];
    //Network Output
    long NetOutput = 0;
    //feature transformer
    FeatureTransformer Transformer = new FeatureTransformer(768, 16);
    const int transformaer_out_size = 16;
    //All the other Layers
    LinearLayer Output = new LinearLayer(32, 1);
    //other stuff
    Vector256<int> kOnes256 = new Vector256<int>();
    short[] initKOnes256 = new short[16];
    int weight_scaling = 4096;
    int activation_scaling = 32767;
    //returns the piecetype of the halfkp encoding
    //piecetype[0] is for black and piecetype[1] is for white
    int[][] PieceType = new int[2][];
    //Log2 of weigtscale
    byte Log2WeightScale = 12;
    standart stuff = new standart();
    public NNUE_avx2(bool LoadNet)
    {
        initTransformer();
        initOutput();

        if (LoadNet && File.Exists("ValueNet.nnue"))
            LoadOldNetFile("ValueNet.nnue");

        //initkOnes256();
        initPtype();

        //initialize the feature vectors
        Features[0] = new List<int>();
        Features[1] = new List<int>();
    }
    public void set_acc_from_position(position InputBoard)
    {
        //load the features for the board into the feature vector
        Features = BoardToHalfP(InputBoard);

        //load the feature vector into the accumulator
        acc = RefreshAcc(Transformer, acc, Features, 0);
        acc = RefreshAcc(Transformer, acc, Features, 1);
    }
    //initialize the four Layers
    public void initTransformer()
    {
        //Weights
        for (int i = 0; i < Transformer.weight.GetLength(0); i++)
        {
            for (int j = 0; j < Transformer.weight.GetLength(1); j++)
            {
                Transformer.weight[i, j] = 1;
            }
        }
        //Biases
        for (int i = 0; i < Transformer.bias.Length; i++)
        {
            Transformer.bias[i] = 1;
        }
    }
    public void initOutput()
    {
        for (int i = 0; i < Output.Input_size; i++)
        {
            Output.weight[i] = 1;
        }
        Output.bias[0] = 1;
    }
    //inittialize the piecetype array
    public List<int>[] BoardToHalfP(position board)
    {
        List<int>[] Features = new List<int>[2];
        for (int color = 0; color <= 1; color++)
        {
            Features[color] = new List<int>();

            for (int square = 0; square < 64; square++)
            {
                if (PieceType[color][board.board[square]] > -1)
                    Features[color].Add(PieceType[color][board.board[square]] + square ^ (color == 1 ? 0 : 56));
            }
        }

        return Features;
    }

    public void initPtype()
    {
        for (byte i = 0; i < 2; i++)
        {
            PieceType[i] = new int[17];
            for (int j = 0; j < 17; j++)
            {
                switch (j)
                {
                    case standart_chess.pawn | 0b0000:
                        PieceType[i][j] = ChangeType(6, i);
                        break;
                    case standart_chess.knight | 0b0000:
                        PieceType[i][j] = ChangeType(7, i);
                        break;
                    case  standart_chess.bishop | 0b0000:
                        PieceType[i][j] = ChangeType(8, i);
                        break;
                    case standart_chess.rook | 0b0000:
                        PieceType[i][j] = ChangeType(9, i);
                        break;
                    case standart_chess.queen | 0b0000:
                        PieceType[i][j] = ChangeType(10, i);
                        break;
                    case standart_chess.king | 0b0000:
                        PieceType[i][j] = ChangeType(11, i);
                        break;
                    case standart_chess.pawn | 0b1000:
                        PieceType[i][j] = ChangeType(0, i);
                        break;
                    case standart_chess.knight | 0b1000:
                        PieceType[i][j] = ChangeType(1, i);
                        break;
                    case standart_chess.bishop | 0b1000:
                        PieceType[i][j] = ChangeType(2, i);
                        break;
                    case standart_chess.rook | 0b1000:
                        PieceType[i][j] = ChangeType(3, i);
                        break;
                    case standart_chess.queen | 0b1000:
                        PieceType[i][j] = ChangeType(4, i);
                        break;
                    case standart_chess.king | 0b1000:
                        PieceType[i][j] = ChangeType(5, i);
                        break;
                    default:
                        PieceType[i][j] = -1;
                        break;
                }
            }
        }
    }
    public int ChangeType(int piecetype, byte color)
    {
        if (color == 0)
        {
            //Black Piece
            if (piecetype - 5 > 0)
                return (piecetype -= 6) * 64;

            return (piecetype += 6) * 64;
        }

        return piecetype * 64;
    }
    //takes in the accumulator and gives back the position value
    public int AccToOutput(Accumulator acc , byte color)
    {
        //add clipped relu to accumulator
        accReluOut = crelu32(32, accReluOut, acc.ReturnSide(color), acc.ReturnSide((byte)(1 - color)));
        //perform matrix multiplication with Output layer
        NetOutput = calculate_output_value(Output, accReluOut, NetOutput);
        //return the Netoutput scaled back and as an integer
        return (int)((NetOutput * 1000) / (weight_scaling * activation_scaling));
    }
    public void update_acc_from_move(position board, reverse_move Move)
    {
        List<int>[] FeaturesToAdd = new List<int>[2];
        List<int>[] FeaturestoRemove = new List<int>[2];
        byte removed_piece_color = (byte)(Move.removed_piece_idx != 0 ? (Move.removed_pieces[0, 1] >> 3) : board.color);

        for (int color = 0; color < 2; color++)
        {
            FeaturesToAdd[color] = new List<int>();
            FeaturestoRemove[color] = new List<int>();

            for (int i = 0; i < Move.removed_piece_idx; i++)
                FeaturestoRemove[color].Add(PieceType[color][Move.removed_pieces[i, 1]] + Move.removed_pieces[i, 0] ^ (color == 1 ? 0 : 56));

            for (int i = 0; i < Move.moved_piece_idx; i++)
            {
                FeaturesToAdd[color].Add(PieceType[color][board.board[Move.moved_pieces[i, 1]]] + Move.moved_pieces[i, 1] ^ (color == 1 ? 0 : 56));
                if (removed_piece_color == board.color) FeaturestoRemove[color].Add(PieceType[color][board.board[Move.moved_pieces[i, 1]]] + Move.moved_pieces[i, 0] ^ (color == 1 ? 0 : 56));
            }

            acc = UpdateAcc(Transformer, acc, FeaturesToAdd, FeaturestoRemove, (byte)color);

        }
    }
    //calculate accumulator values from start
    unsafe public Accumulator RefreshAcc(FeatureTransformer transformer, Accumulator acc, List<int>[] Features, byte color)
    {
        const int register_width = 8;
        //Size of the Feature Transformer Output size (16) divided by the register width (8)
        const int NumberofChunks = transformaer_out_size / register_width;
        //Generate the avx2 registers
        Vector256<int>[] registers = new Vector256<int>[NumberofChunks];

        //Load the bias into the registers
        for (int i = 0; i < NumberofChunks; i++)
        {
            //get the address of the bias
            fixed (int* currentAddress = &transformer.bias[i * register_width]) 
            {
                //load this part of the register with the data of the address
                registers[i] = Avx2.LoadVector256(currentAddress);
            }
        }

        //Add the weights
        foreach(int Place in Features[color])
        {
            for (int i = 0; i < NumberofChunks; i++)
            {
                //get the address of the weights
                fixed (int* currentAddress = &transformer.weight[Place, i * register_width]) 
                {
                    //add the weights withe the register
                    registers[i] = Avx2.Add(registers[i], Avx2.LoadVector256(currentAddress));
                }
            }
        }

        //store the registers into the accumulator
        for (int i = 0; i < NumberofChunks; i++)
        {
            //get the address of the accumulator
            fixed (int* currentAddress = &acc.Acc[color][i * register_width]) 
            {
                //store the register ath this address
                Avx2.Store(currentAddress, registers[i]);
            }
        }
        return acc;
    }
    //just update the accumulator values
    unsafe public Accumulator UpdateAcc(FeatureTransformer transformer, Accumulator acc, List<int>[] AddedFeatures, List<int>[] RemovedFeatures, byte color)
    {
        const int register_width = 8;
        //Size of the Feature Transformer Output size (16) divided by the register width (8)
        const int NumberofChunks = transformaer_out_size / register_width;
        //Generate the avx2 registers
        Vector256<int>[] registers = new Vector256<int>[NumberofChunks];

        //Load the old accumulator into the registers
        for (int i = 0; i < NumberofChunks; i++)
        {
            //get the address of the accumulator
            fixed (int* currentAddress = &acc.Acc[color][i * register_width])
            {
                //load this part of the register with the data of the address
                registers[i] = Avx2.LoadVector256(currentAddress);
            }
        }

        //Remove the old features 
        foreach (int Place in RemovedFeatures[color])
        {
            for (int i = 0; i < NumberofChunks; i++)
            {
                //get the address of the weights
                fixed (int* currentAddress = &transformer.weight[Place, i * register_width])
                {
                    //add the weights withe the register
                    registers[i] = Avx2.Subtract(registers[i], Avx2.LoadVector256(currentAddress));
                }
            }
        }

        //Add the new features
        foreach (int Place in AddedFeatures[color])
        {
            for (int i = 0; i < NumberofChunks; i++)
            {
                //get the address of the weights
                fixed (int* currentAddress = &transformer.weight[Place, i * register_width])
                {
                    //add the weights withe the register
                    registers[i] = Avx2.Add(registers[i], Avx2.LoadVector256(currentAddress));
                }
            }
        }

        //store the registers into the accumulator
        for (int i = 0; i < NumberofChunks; i++)
        {
            //get the address of the accumulator
            fixed (int* currentAddress = &acc.Acc[color][i * register_width])
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
    unsafe long calculate_output_value(LinearLayer layer, short[] Input, long Output)
    {
        const int register_width = 16;
        int num_in_chunks = Input.Length / register_width;
        Vector256<short> In;
        Vector256<short> layer_matrix;
        Output = 0;

        int[] intermediate_array = new int[register_width / 2];

        for (int i = 0; i < num_in_chunks; i++)
        {

            fixed (short* currentAddress = &Input[register_width * i])
            {
                In = Avx2.LoadVector256(currentAddress);
            }

            fixed (short* currentAddress = &layer.weight[register_width * i])
            {
                layer_matrix = Avx2.LoadVector256(currentAddress);
            }

            Vector256<int> out_vector = Avx2.MultiplyAddAdjacent(In, layer_matrix);

            fixed (int* currentAddress = &intermediate_array[0])
            {
                Avx2.Store(currentAddress, out_vector);
            }

            foreach (int value in intermediate_array)
                Output += value;
        }

        return (Output + layer.bias[0]);
    }
    //clipped relu for the accumulator output
    unsafe public byte[] crelu16(int size , byte[] Output , short[] InputA , short[] InputB)
    {
        const int in_register_width = 256 / 16;
        const int out_register_width = 256 / 8;
        int num_out_chunks = size / ( 2 * out_register_width);

        Vector256<sbyte> zero = new Vector256<sbyte>();
        byte control = 0b11011000;
        //control = 0b00100111;
        for (int i = 0; i < num_out_chunks; i++)
        {
            //calucalte for InputA
            Vector256<short> in0, in1;
            fixed (short* currentPointer0 = &InputA[in_register_width * (i * 2 + 0)], currentPointer1 = &InputA[in_register_width * (i * 2 + 1)]) 
            {
                in0 = Avx2.ShiftRightArithmetic(Avx2.LoadVector256(currentPointer0), Log2WeightScale);
                in1 = Avx2.ShiftRightArithmetic(Avx2.LoadVector256(currentPointer1), Log2WeightScale);
            }

            Vector256<byte> resultA = Avx2.Permute4x64(Avx2.Max(Avx2.PackSignedSaturate(in0, in1), zero).AsInt64(), control).AsByte();

            //calculate for InputB
            Vector256<short> in2, in3;
            fixed (short* currentPointer2 = &InputB[in_register_width * (i * 2 + 0)], currentPointer3 = &InputB[in_register_width * (i * 2 + 1)])
            {
                in2 = Avx2.ShiftRightArithmetic(Avx2.LoadVector256(currentPointer2), Log2WeightScale);
                in3 = Avx2.ShiftRightArithmetic(Avx2.LoadVector256(currentPointer3), Log2WeightScale);
            }

            Vector256<byte> resultB = Avx2.Permute4x64(Avx2.Max(Avx2.PackSignedSaturate(in2, in3), zero).AsInt64(), control).AsByte();

            fixed (byte* currentPointerA = &Output[i * out_register_width] , currentPointerB = &Output[i * out_register_width + size / 2])
            {
                Avx2.Store(currentPointerA, resultA);
                Avx2.Store(currentPointerB, resultB);
            }
        }

        return Output;
    }
    //clipped relu for the linear layer Outputs
    unsafe public short[] crelu32(int size, short[] Output, int[] InputA , int[] InputB)
    {
        const int in_register_width = 256 / 32;
        const int out_register_width = 256 / 16;
        int num_out_chunks = size / (2 * out_register_width);
        byte control = 0b11011000;

        Vector256<short> zero = new Vector256<short>();

        for (int i = 0; i < num_out_chunks; i++)
        {
            Vector256<short> in0;
            fixed (int* PointerA = &InputA[in_register_width * (i * 2 + 0)], PointerB = &InputA[in_register_width * (i * 2 + 1)]) 
            {
                in0 = Avx2.PackSignedSaturate(Avx2.ShiftRightArithmetic(Avx2.LoadVector256(PointerA), Log2WeightScale), Avx2.ShiftRightArithmetic(Avx2.LoadVector256(PointerB), Log2WeightScale));
            }

            Vector256<short> resultA = Avx2.Permute4x64(Avx2.Max(in0, zero).AsInt64(), control).AsInt16();

            Vector256<short> in1;
            fixed (int* PointerA = &InputB[in_register_width * (i * 2 + 0)], PointerB = &InputB[in_register_width * (i * 2 + 1)])
            {
                in1 = Avx2.PackSignedSaturate(Avx2.ShiftRightArithmetic(Avx2.LoadVector256(PointerA), Log2WeightScale), Avx2.ShiftRightArithmetic(Avx2.LoadVector256(PointerB), Log2WeightScale));
            }

            Vector256<short> resultB = Avx2.Permute4x64(Avx2.Max(in1, zero).AsInt64(), control).AsInt16();

            fixed (short* PointerA = &Output[i * out_register_width] , PointerB = &Output[i * out_register_width + (size / 2)])
            {
                Avx2.Store(PointerA, resultA);
                Avx2.Store(PointerB, resultB);
            }
        }

        return Output;
    }
    public void LoadOldNetFile(string filename)
    {
        StreamReader sr = new StreamReader(filename);

        //Weights

        //Output
        load_layer_weights(Output, sr);

        //Biases

        //Output
        load_layer_bias(Output, sr);

        //Transformer
        for (int i = 0; i < Transformer.weight.GetLength(0); i++)
        {
            for (int j = 0; j < Transformer.weight.GetLength(1); j++)
            {
                byte[] arr = new byte[4];
                for (int k = 0; k < arr.Length; k++)
                    arr[k] = (byte)sr.Read();

                Transformer.weight[i, j] = BitConverter.ToInt32(arr);
            }
        }

        //Transformer bias
        for (int i = 0; i < Transformer.bias.Length; i++)
        {
            byte[] arr = new byte[4];
            for (int k = 0; k < arr.Length; k++)
                arr[k] = (byte)sr.Read();

            Transformer.bias[i] = BitConverter.ToInt32(arr);
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
    public float MaxMinInt(float Input)
    {
        return (float)Math.Max(int.MinValue, Math.Min(int.MaxValue, Math.Round(Input, 0)));
    }
    public float MaxMinShort(float Input)
    {
        return (float)Math.Max(short.MinValue, Math.Min(short.MaxValue, Math.Round(Input, 0)));
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
    public LinearLayer(int ColumnSize , int RowSize)
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
    public FeatureTransformer(int ColumnSize , int RowSize)
    {
        weight = new int[ColumnSize, RowSize];
        bias = new int[RowSize];
    }
}