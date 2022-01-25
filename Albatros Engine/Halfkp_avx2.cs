using System;
using System.Collections.Generic;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.IO;
class Halfkp_avx2
{
    //kingsquares
    public int[] kingsquares = new int[4], kingpositions = new int[2];
    //input into the feature transformer
    public List<int>[] Features = new List<int>[2];
    //output of the Feature transformers
    public Accumulator acc = new Accumulator(256);
    //acc gone trough the relu layer
    public byte[] accReluOut = new byte[512];
    //feature transformer
    FeatureTransformer Transformer = new FeatureTransformer(40960, 256);
    //All the other Layers
    LinearLayer L1 = new LinearLayer(512, 32), L2 = new LinearLayer(32, 32), Output = new LinearLayer(32, 1);
    //layer outputs
    int[] L1Out = new int[32], L2Out = new int[32];
    int NetOutput;
    //clipped relu outputs
    byte[] L1creluOut = new byte[32], L2creluOut = new byte[32];
    //other stuff
    Vector256<short> kOnes256 = new Vector256<short>();
    short[] initKOnes256 = new short[16];
    int weight_scaling = 64;
    int activation_scaling = 127;
    //returns the piecetype of the halfkp encoding
    //piecetype[0] is for black and piecetype[1] is for white
    int[][] PieceType = new int[2][];
    //Log2 of weigtscale
    byte Log2WeightScale = 6;
    public Halfkp_avx2(byte[,] board , int[] kingpos)
    {
        initTransformer();
        initL1();
        initL2();
        initOutput();
        try
        {
            LoadOldNetFile("ValueNet.nnue");
        }
        catch { }
        initkOnes256();
        initPtype();
        //initialize the feature vectors
        Features[0] = new List<int>();
        Features[1] = new List<int>();
        set_acc_from_position(board, kingpos);
    }
    public void set_acc_from_position(byte[,] InputBoard , int[]Kingplace)
    {
        update_king_squares(Kingplace);
        //load the features for the board into the feature vector
        GetFeaturesFromPos(InputBoard, 0, Kingplace[0], Kingplace[1]);
        GetFeaturesFromPos(InputBoard, 1, Kingplace[2], Kingplace[3]);
        //load the featurvector into the accumulator
        acc = RefreshAcc(Transformer, acc, Features, 0);
        acc = RefreshAcc(Transformer, acc, Features, 1);
    }
    public void update_king_squares(int[] kingposition)
    {
        kingsquares = kingposition;
        for (int color = 0; color < 2; color++)
            kingpositions[color] = ((7 * (1 - color) + (2 * color - 1) * (kingsquares[1 + 2 * color] - 1)) * 8 + kingsquares[0 + 2 * color] - 1) * 64;
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
    public void initL1()
    {
        //weights
        for (int i = 0; i < L1.Input_size; i++)
        {
            for (int j = 0; j < L1.Output_size; j++)
            {
                L1.weight[i * L1.Output_size + j] = 1;
            }
        }
        //biases
        for (int i = 0; i < L1.Output_size; i++)
        {
            L1.bias[i] -= 1;
        }
    }
    public void initL2()
    {
        //weights
        for (int i = 0; i < L2.Input_size; i++)
        {
            for (int j = 0; j < L2.Output_size; j++)
            {
                L2.weight[i * L2.Output_size + j] = (sbyte)i;
            }
        }
        //biases
        for (int i = 0; i < L2.Output_size; i++)
        {
            L2.bias[i] -= 32 * i;
        }
    }
    //inittialize the piecetype array
    public void initPtype()
    {
        for (byte i = 0; i < 2; i++)
        {
            PieceType[i] = new int[27];
            for (int j = 0; j < 27; j++)
            {
                switch (j)
                {
                    case 0b00000001:
                        PieceType[i][j] = ChangeType(5, i);
                        break;
                    case 0b00000010:
                        PieceType[i][j] = ChangeType(5, i);
                        break;
                    case 0b00000011:
                        PieceType[i][j] = ChangeType(5, i);
                        break;
                    case 0b00000100:
                        PieceType[i][j] = ChangeType(6, i);
                        break;
                    case 0b00000101:
                        PieceType[i][j] = ChangeType(7, i);
                        break;
                    case 0b00001000:
                        PieceType[i][j] = ChangeType(8, i);
                        break;
                    case 0b00001001:
                        PieceType[i][j] = ChangeType(9, i);
                        break;
                    case 0b00001010:
                        PieceType[i][j] = ChangeType(9, i);
                        break;
                    case 0b00010001:
                        PieceType[i][j] = ChangeType(0, i);
                        break;
                    case 0b00010010:
                        PieceType[i][j] = ChangeType(0, i);
                        break;
                    case 0b00010011:
                        PieceType[i][j] = ChangeType(0, i);
                        break;
                    case 0b00010100:
                        PieceType[i][j] = ChangeType(1, i);
                        break;
                    case 0b00010101:
                        PieceType[i][j] = ChangeType(2, i);
                        break;
                    case 0b00011000:
                        PieceType[i][j] = ChangeType(3, i);
                        break;
                    case 0b00011001:
                        PieceType[i][j] = ChangeType(4, i);
                        break;
                    case 0b00011010:
                        PieceType[i][j] = ChangeType(4, i);
                        break;
                    case 0b00000110:
                        PieceType[i][j] = -2;
                        break;
                    case 0b00000111:
                        PieceType[i][j] = -2;
                        break;
                    case 0b00010111:
                        PieceType[i][j] = -2;
                        break;
                    case 0b00010110:
                        PieceType[i][j] = -2;
                        break;
                    default:
                        PieceType[i][j] = -1;
                        break;
                }
            }
        }
    }
    public int ChangeType(int piecetype , byte color)
    {
        if (color == 0)
        {
            //Black Piece
            if (piecetype - 4 > 0)
                return (piecetype -= 5) * 4096;
            else
                return (piecetype += 5) * 4096;
        }
        else
            return piecetype * 4096;
    }
    //test the avx2 Functions
    public void Test_RefreshAccandUpdateAcc()
    {
        List<int>[] Toadd = new List<int>[2];
        List<int>[] Toremove = new List<int>[2];
        for (int i = 0; i < 2; i++) 
        {
            Toadd[i] = new List<int>();
            Toremove[i] = new List<int>();
        }
        Toadd[0].Add(42);
        Toadd[1].Add(31);
        Toadd[1].Add(10);
        Toremove[0].Add(1);
        Toremove[0].Add(2);
        Toremove[1].Add(1);
        Features[0].Add(1);
        Features[0].Add(2);
        Features[1].Add(1);
        acc = RefreshAcc(Transformer, acc, Features, 0);
        acc = RefreshAcc(Transformer, acc, Features, 1);
        string Output = "";
        for (int i = 0; i < acc.Acc[0].Length; i++)
        {
            Output += acc.ReturnSide(0)[i]+ " ";
        }
        Console.WriteLine("values of black: " + Output);
        Console.WriteLine("should all be 2");
        Output = "";
        for (int i = 0; i < acc.Acc[1].Length; i++)
        {
            Output += acc.ReturnSide(1)[i] + " ";
        }
        Console.WriteLine("values of white: " + Output);
        Console.WriteLine("should all be 1");
        acc = UpdateAcc(Transformer, acc, Toadd, Toremove, 0);
        acc = UpdateAcc(Transformer, acc, Toadd, Toremove, 1);
        Output = "";
        for (int i = 0; i < acc.Acc[0].Length; i++)
        {
            Output += acc.ReturnSide(0)[i] + " ";
        }
        Console.WriteLine("values of black: " + Output);
        Console.WriteLine("should all be 1");
        Output = "";
        for (int i = 0; i < acc.Acc[1].Length; i++)
        {
            Output += acc.ReturnSide(1)[i] + " ";
        }
        Console.WriteLine("values of white: " + Output);
        Console.WriteLine("should all be 2");
    }
    public void Test_LinearLayer()
    {
        byte[] Input = new byte[32];
        int[] Output = new int[32];
        string printout = "";

        for (int i = 0; i < 32; i++)
            Input[i] = 1;

        Output = LinearLayer(L2, Input, Output);

        foreach(int value in Output)
        {
            printout += value + " ";
        }
        Console.WriteLine("the values are " + printout);
        Console.WriteLine("they should all be 0");
    }
    public void Test_crelu()
    {
        int[] InputA = new int[512];
        short[] InputB = new short[256];
        short[] InputC = new short[256];
        byte[] OutputA = new byte[512];
        byte[] OutputB = new byte[512];
        string Output = "";
        for (int i = 0; i < 512; i++)
        {
            InputA[i] = 200;
            if (i < 256)
                InputB[i] = -1;
            else
                InputC[i - 256] = 12800;
        }
        OutputA = crelu32(512, OutputA, InputA);
        OutputB = crelu16(512, OutputB, InputB , InputC);
        for (int i = 0; i < 512; i++)
            Output += OutputA[i] + " ";
        Console.WriteLine("the Output  of crelu32 is " + Output);
        Console.WriteLine("they should all be 127");
        Output = "";
        for (int i = 0; i < 512; i++)
            Output += OutputB[i] + " ";
        Console.WriteLine("the Output of crelu16 is " + Output);
        Console.WriteLine("they should all be 0");

    }
    public void printLayerOutput(byte[] input_vector)
    {
        string Output = "";
        int counter = 0;
        foreach (int Value in input_vector)
        {
            Output += Math.Round(Convert.ToSingle(Value) / 127, 2) + " ";
            counter++;
            if (counter == 256)
                Output += "\n256: \n";
        }
        Console.WriteLine("\nThe vector values are:\n {0}", Output);
    }
    //takes in the accumulator and gives back the position value
    public float AccToOutput(Accumulator acc , byte color)
    {
        //add clipped relu to accumulator
        accReluOut = crelu16(512, accReluOut, acc.ReturnSide((byte)(1 - color)), acc.ReturnSide(color));
        //permorm the L1 matrix multiplication
        L1Out = LinearLayer(L1, accReluOut, L1Out);
        //perform clipped relu on L1 output
        L1creluOut = crelu32(32, L1creluOut, L1Out);
        //perform the L2 matrix multiplication
        L2Out = LinearLayer(L2, L1creluOut, L2Out);
        //perform clipped relu on L2 output
        L2creluOut = crelu32(32, L2creluOut, L2Out);
        //perform matrix multiplication with Output layer
        NetOutput = calculate_output_value(Output, L2creluOut, NetOutput);
        //return the Netoutput scaled back and as a float
        return Convert.ToSingle(NetOutput) / (weight_scaling * activation_scaling);
    }
    //calculate the feature vector for a color fom an InputBoard
    public void test_function_a(byte[,] InputBoard, byte color, int[] Kingplace, int[] move)
    {
        Accumulator test_a = new Accumulator(256), test_b = new Accumulator(256) , backup = new Accumulator(256);
        Array.Copy(acc.Acc, backup.Acc, test_a.Acc.Length);
        set_acc_from_position(InputBoard, Kingplace);
        Array.Copy(acc.Acc, test_a.Acc, test_a.Acc.Length);
        Array.Copy(backup.Acc, acc.Acc, test_a.Acc.Length);
        update_acc_from_move(InputBoard, move, color);
        Array.Copy(acc.Acc, test_b.Acc, test_a.Acc.Length);
        Array.Copy(backup.Acc, acc.Acc, test_a.Acc.Length);
        for (int i = 0; i < 2; i++) 
        {
            for (int j = 0; j < 256; j++)
            {
                if (test_a.Acc[i][j] != test_b.Acc[i][j])
                    Console.WriteLine("it does not work !");
                else
                    Console.WriteLine("it does work");
            }
        }
    }
    public void GetFeaturesFromPos(byte[,] InputBoard , byte color , int KingX , int KingY)
    {
        kingsquares[2 * color + 0] = KingX;
        kingsquares[2 * color + 1] = KingY;
        update_king_squares(kingsquares);
        Features[color] = new List<int>();
        for (int i = 1; i < 9; i++)
        {
            for (int j = 1; j < 9; j++)
            {
                if (PieceType[color][InputBoard[i, j]] > -1)
                {
                    Features[color].Add(PieceType[color][InputBoard[i, j]] + kingpositions[color] + (7 * (1 - color) + (2 * color - 1) * (j - 1)) * 8 + i - 1);
                }
            }
        }
    }
    public void update_acc_from_move(byte[,] InputBoard, int[] Move, byte color)
    {
        List<int>[] FeaturesToAdd = new List<int>[2];
        List<int>[] FeaturestoRemove = new List<int>[2];
        for (int j = 0; j < 2; j++)
        {
            FeaturesToAdd[j] = new List<int>();
            FeaturestoRemove[j] = new List<int>();
            for (int i = 0; i < Move.Length / 3; i++)
            {     
                //normal piece
                if (PieceType[j][Move[i * 3 + 2]] > -1)
                {
                    if (Move.Length >= (i + 2) * 3)
                        FeaturesToAdd[j].Add(PieceType[j][Move[i * 3 + 2]] + kingpositions[j] + (7 * (1 - j) + (2 * j - 1) * (Move[i * 3 + 4] - 1)) * 8 + Move[i * 3 + 3] - 1);
                    FeaturestoRemove[j].Add(PieceType[j][Move[i * 3 + 2]] + kingpositions[j] + (7 * (1 - j) + (2 * j - 1) * (Move[i * 3 + 1] - 1)) * 8 + Move[i * 3 + 0] - 1);
                }
                //king of type color
                else if (PieceType[color][Move[i * 3 + 2]] == -2)
                {
                    kingsquares[2 * color + 0] = Move[i * 3 + 3];
                    kingsquares[2 * color + 1] = Move[i * 3 + 4];
                    GetFeaturesFromPos(InputBoard, color, Move[i * 3 + 3], Move[i * 3 + 4]);

                }
            }
            acc = UpdateAcc(Transformer, acc, FeaturesToAdd, FeaturestoRemove, (byte)j);
        }
    }
    //calculate accumulator values from start
    unsafe public Accumulator RefreshAcc(FeatureTransformer transformer, Accumulator acc, List<int>[] Features, byte color)
    {
        const int register_width = 16;
        //Size of the Feature Transformer Output size (256) divided by the register width (16)
        const int NumberofChunks = 16;
        //Generate the avx2 registers
        Vector256<short>[] registers = new Vector256<short>[NumberofChunks];

        //Load the bias into the registers
        for (int i = 0; i < NumberofChunks; i++)
        {
            //get the address of the bias
            fixed (short* currentAddress = &transformer.bias[i * register_width]) 
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
                fixed (short* currentAddress = &transformer.weight[Place, i * register_width]) 
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
            fixed (short* currentAddress = &acc.Acc[color][i * register_width]) 
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
        const int register_width = 16;
        //Size of the Feature Transformer Output size (256) divided by the register width (16)
        const int NumberofChunks = 16;
        //Generate the avx2 registers
        Vector256<short>[] registers = new Vector256<short>[NumberofChunks];

        //Load the old accumulator into the registers
        for (int i = 0; i < NumberofChunks; i++)
        {
            //get the address of the accumulator
            fixed (short* currentAddress = &acc.Acc[color][i * register_width])
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
                fixed (short* currentAddress = &transformer.weight[Place, i * register_width])
                {
                    //add the weights withe the register
                    registers[i] = Avx2.Subtract(registers[i], Avx2.LoadVector256(currentAddress));
                }
            }
        }

        //Add the weights
        foreach (int Place in AddedFeatures[color])
        {
            for (int i = 0; i < NumberofChunks; i++)
            {
                //get the address of the weights
                fixed (short* currentAddress = &transformer.weight[Place, i * register_width])
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
            fixed (short* currentAddress = &acc.Acc[color][i * register_width])
            {
                //store the register ath this address
                Avx2.Store(currentAddress, registers[i]);
            }
        }

        return acc;
    }
    //do the Matrix multplication for the linear layers
    unsafe int[] LinearLayer(LinearLayer layer, byte[] Input, int[] Output)
    {
        //One register size is 256 bits it isfilled up with signed int8 s so the size is 256 / 8 = 32  
        const int register_width = 32;
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

                fixed (sbyte* currentAdress = &layer.weight[offset0 + j * register_width])
                {
                    sum0 = m256_add_dpbusd_epi32(sum0, In, Avx2.LoadVector256(currentAdress));
                }
                fixed (sbyte* currentAdress = &layer.weight[offset1 + j * register_width])
                {
                    sum1 = m256_add_dpbusd_epi32(sum1, In, Avx2.LoadVector256(currentAdress));
                }
                fixed (sbyte* currentAdress = &layer.weight[offset2 + j * register_width])
                {
                    sum2 = m256_add_dpbusd_epi32(sum2, In, Avx2.LoadVector256(currentAdress));
                }
                fixed (sbyte* currentAdress = &layer.weight[offset3 + j * register_width])
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
    unsafe int calculate_output_value(LinearLayer layer, byte[] Input, int Output)
    {
        const int register_width = 32;
        Vector256<byte> In;
        Vector256<sbyte> layer_matrix;
        Output = 0;

        short[] intermediate_array = new short[register_width / 2];

        fixed (byte* currentAddress = &Input[0])
        {
            In = Avx2.LoadVector256(currentAddress);
        }

        fixed(sbyte* currentAddress = &layer.weight[0])
        {
            layer_matrix = Avx2.LoadVector256(currentAddress);
        }

        Vector256<short> out_vector = Avx2.MultiplyAddAdjacent(In, layer_matrix);

        fixed(short* currentAddress = &intermediate_array[0])
        {
            Avx2.Store(currentAddress, out_vector);
        }

        foreach (short value in intermediate_array)
            Output += value;

        return (Output + layer.bias[0]);
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
    public Vector256<int> m256_add_dpbusd_epi32(Vector256<int> source , Vector256<byte> a , Vector256<sbyte> b)
    {

        // Multiply a * b and accumulate neighbouring outputs into int16 values
        Vector256<short> product = Avx2.MultiplyAddAdjacent(a, b);

        // Multiply product0 by 1 (idempotent) and accumulate neighbouring outputs into int32 values
        Vector256<int> product0 = Avx2.MultiplyAddAdjacent(product, kOnes256);

        // Add to the main int32 accumulator.
        return Avx2.Add(source, product0);
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
    unsafe public byte[] crelu32(int size, byte[] Output, int[] Input)
    {
        const int in_register_width = 256 / 32;
        const int out_register_width = 256 / 8;
        int num_out_chunks = size / out_register_width;
        int[] ControlValues = new int[] { 0, 4, 1, 5, 2, 6, 3, 7 };

        Vector256<sbyte> zero = new Vector256<sbyte>();
        Vector256<int> control;
        fixed(int* currentPointer = &ControlValues[0])
        {
            control = Avx2.LoadVector256(currentPointer);
        }

        for (int i = 0; i < num_out_chunks; i++)
        {
            Vector256<short> in0;
            fixed (int* PointerA = &Input[in_register_width * (i * 4 + 0)], PointerB = &Input[in_register_width * (i * 4 + 1)]) 
            {
                in0 = Avx2.PackSignedSaturate(Avx2.LoadVector256(PointerA), Avx2.LoadVector256(PointerB));
            }
            Vector256<short> in1;
            fixed (int* PointerA = &Input[in_register_width * (i * 4 + 2)], PointerB = &Input[in_register_width * (i * 4 + 3)]) 
            {
                in1 = Avx2.PackSignedSaturate(Avx2.LoadVector256(PointerA), Avx2.LoadVector256(PointerB));
            }
            Vector256<sbyte> inbetween = Avx2.Max(Avx2.PackSignedSaturate(in0, in1), zero);
            Vector256<byte> result = Avx2.PermuteVar8x32(Avx2.Max(Avx2.PackSignedSaturate(in0, in1), zero).AsInt32(), control).AsByte();

            fixed (byte* currentPointer = &Output[i * out_register_width])
            {
                Avx2.Store(currentPointer, result);
            }
        }

        return Output;
    }
    public void LoadOldNetFile(string filename)
    {
        int counter = 1;
        StreamReader sr = new StreamReader(filename);
        string FileOutput = sr.ReadToEnd();
        sr.Close();
        string[] Values;

        Values = FileOutput.Split(' ');

        //Weights
        //L1
        for (int i = 0; i < L1.Input_size; i++)
        {
            for (int j = 0; j < L1.Output_size; j++) 
            {
                L1.weight[j * L1.Input_size + i] = Convert.ToSByte(BitConverter.Int32BitsToSingle(Convert.ToInt32(Values[counter])) * weight_scaling);
                counter++;
            }
        }
        //L2
        for (int i = 0; i < L2.Input_size; i++)
        {
            for (int j = 0; j < L2.Output_size; j++)
            {
                L2.weight[j * L2.Input_size + i] = Convert.ToSByte(BitConverter.Int32BitsToSingle(Convert.ToInt32(Values[counter])) * weight_scaling);
                counter++;
            }
        }
        //Output
        for (int i = 0; i < Output.weight.Length; i++)
        {
            Output.weight[i] = Convert.ToSByte(BitConverter.Int32BitsToSingle(Convert.ToInt32(Values[counter])) * weight_scaling);
            counter++;
        }
        //Biases
        //L1
        for (int i = 0; i < L1.bias.Length; i++)
        {
            L1.bias[i] = Convert.ToInt16(MaxMin(BitConverter.Int32BitsToSingle(Convert.ToInt32(Values[counter])) * weight_scaling * activation_scaling));
            counter++;
        }
        //L2
        for (int i = 0; i < L2.bias.Length; i++)
        {
            L2.bias[i] = Convert.ToInt16(MaxMin(BitConverter.Int32BitsToSingle(Convert.ToInt32(Values[counter])) * weight_scaling * activation_scaling));
            counter++;
        }
        //Output
        for (int i = 0; i < Output.bias.Length; i++)
        {
            Output.bias[i] = Convert.ToInt16(MaxMin(BitConverter.Int32BitsToSingle(Convert.ToInt32(Values[counter])) * weight_scaling * activation_scaling));
            counter++;
        }

        //StartMatrix
        for (int i = 0; i < Transformer.weight.GetLength(0); i++)
        {
            for (int j = 0; j < Transformer.weight.GetLength(1); j++)
            {
                Transformer.weight[i, j] = Convert.ToInt16(MaxMin(BitConverter.Int32BitsToSingle(Convert.ToInt32(Values[counter])) * weight_scaling * activation_scaling));
                counter++;
            }
        }

        //StartMatrixBias
        for (int i = 0; i < Transformer.bias.Length; i++)
        {
            Transformer.bias[i] = Convert.ToInt16(MaxMin(BitConverter.Int32BitsToSingle(Convert.ToInt32(Values[counter])) * weight_scaling * activation_scaling));
            counter++;
        }

        Console.WriteLine("Done !");
    }
    public float MaxMin(float Input)
    {
        return (float)Math.Min(short.MinValue , Math.Max(short.MaxValue, Math.Round(Input, 0)));
    }
}
class Accumulator
{
    //Accumulator (Acc[0] White, Acc[1] Black)
    public short[][] Acc;

    public Accumulator(int Size)
    {
        Acc = new short[2][];
        Acc[0] = new short[Size];
        Acc[1] = new short[Size];
    }

    public short[] ReturnSide(byte color)
    {
        return Acc[color];
    }
}
class LinearLayer
{
    public sbyte[] weight;
    public int[] bias;
    public int Input_size = 0, Output_size = 0;
    public LinearLayer(int ColumnSize , int RowSize)
    {
        Input_size = ColumnSize;
        Output_size = RowSize;
        weight = new sbyte[ColumnSize * RowSize];
        bias = new int[RowSize];
    }
}
class FeatureTransformer
{
    public short[,] weight;
    public short[] bias;
    public FeatureTransformer(int ColumnSize , int RowSize)
    {
        weight = new short[ColumnSize, RowSize];
        bias = new short[RowSize * 2];
    }
}