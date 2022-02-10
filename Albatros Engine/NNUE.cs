using System;
using System.IO;
using System.Collections.Generic;
class NNUE
{
    Random random = new Random();
    //HalfKaV2
    public float[,] StartMatrix = new float[45056, 520];
    public float[,] MatrixChange = new float[45056, 520];
    public float[,] OldMatrix = new float[45056, 520];
    int Piececount;
    public float[][][,] Weigths = new float[8][][,];
    public float[][][,] WeigthChanges = new float[8][][,];
    public float[][][,] OldWeigths = new float[8][][,];
    public float[][] NeuronInput = new float[3][];
    public float[][] NeuronVal = new float[3][];
    public float[][] NeuronErrors = new float[3][];
    public float[][][] Biases = new float[8][][];
    public float[][][] OldBiases = new float[8][][];
    public float[][][] BiasChange = new float[8][][];
    public float[] MatrixOut = new float[1024];
    public float PerspectiveOur = 0, PerspectiveTheir = 0;
    public float OutMatrix = 0, OutNet = 0;
    int[] HalfKav2Format = new int[] { 1024, 16, 32, 1 };
    //HalfKp
    //feature transformer
    public double[,] HalfkpMatrix = new double[40960, 256], HalfkpMatrixChange = new double[40960, 256], HalfkpMatrix_momentum = new double[40960, 256], HalfkpMatrix_old_momentum = new double[40960, 256], HalfkpMatrix_velocity = new double[40960, 256], HalfkpMatrix_lookahead = new double[40960, 256], HalfkpMatrix_velocity_max = new double[40960, 256];
    //featur transformer bias
    public double[] HalfkpMatrixBias = new double[512], HalfkpMatrixBiasChange = new double[512], HalfkpMatrixBias_momentum = new double[512], HalfkpMatrixBias_old_momentum = new double[512], HalfkpMatrixBias_velocity = new double[512], HalfkpMatrixBias_lookahead = new double[512], HalfkpMatrixBias_velocity_max = new double[512];
    //weights
    public double[][,] HalfkpWeigths = new double[3][,], HalfkpWeigthChanges = new double[3][,], HalfkpWeigth_momentum = new double[3][,], HalfkpWeigth_old_momentum = new double[3][,], HalfkpWeigth_velocity = new double[3][,], HalfkpWeigth_lookahead = new double[3][,], HalfkpWeigth_velocity_max = new double[3][,];
    //biases
    public double[][] HalfkpBiases = new double[3][], HalfkpBias_momentum = new double[3][], HalfkpBias_old_momentum = new double[3][], HalfkpBias_velocity = new double[3][], HalfkpBias_velocity_max = new double[3][], HalfkpBias_lookahead = new double[3][], HalfkpBiasChange = new double[3][];

    public double[][] HalfkpNeuronInput = new double[3][], HalfkpNeuronVal = new double[3][], HalfkpNeuronErrors = new double[3][];
    public double[] HalfkpMatrixOut = new double[512];

    public double HalfkpOutNet = 0, iterationcount = 0;
    int[] HalfkpFormat = new int[] { 512, 32, 32, 1 };
    public NNUE()
    {
        // init HalfKav2 Biases
        for (int i = 0; i < Biases.Length; i++)
        {
            Biases[i] = new float[3][];
            BiasChange[i] = new float[3][];
            OldBiases[i] = new float[3][];
            for (int j = 0; j < Biases[i].Length; j++)
            {
                BiasChange[i][j] = new float[HalfKav2Format[j + 1]];
                OldBiases[i][j] = new float[HalfKav2Format[j + 1]];
                Biases[i][j] = new float[HalfKav2Format[j + 1]];
                NeuronVal[j] = new float[HalfKav2Format[j + 1]];
                NeuronInput[j] = new float[HalfKav2Format[j + 1]];
                NeuronErrors[j] = new float[HalfKav2Format[j + 1]];
                for (int k = 0; k < Biases[i][j].Length; k++)
                {
                    Biases[i][j][k] = Convert.ToSingle(((float)random.NextDouble() - 0.5) * 1 / 2);
                }
            }
        }
        //init HalfKp Biases
        for (int i = 0; i < HalfkpBiases.Length; i++)
        {
            HalfkpBiases[i] = new double[HalfkpFormat[i + 1]];
            HalfkpBias_momentum[i] = new double[HalfkpFormat[i + 1]];
            HalfkpBias_old_momentum[i] = new double[HalfkpFormat[i + 1]];
            HalfkpBias_velocity[i] = new double[HalfkpFormat[i + 1]];
            HalfkpBias_velocity_max[i] = new double[HalfkpFormat[i + 1]];
            HalfkpBias_lookahead[i] = new double[HalfkpFormat[i + 1]];
            HalfkpBiasChange[i] = new double[HalfkpFormat[i + 1]];
            HalfkpNeuronVal[i] = new double[HalfkpFormat[i + 1]];
            HalfkpNeuronInput[i] = new double[HalfkpFormat[i + 1]];
            HalfkpNeuronErrors[i] = new double[HalfkpFormat[i + 1]];
            for (int j = 0; j < HalfkpBiases[i].Length; j++)
            {
                HalfkpBiases[i][j] = Convert.ToSingle(((float)random.NextDouble() - 0.5) * 1 / 2);
            }
        }
        // init HalfKav2 Weights
        for (int i = 0; i < Weigths.Length; i++)
        {
            OldWeigths[i] = new float[3][,];
            WeigthChanges[i] = new float[3][,];
            Weigths[i] = new float[3][,];
            for (int j = 0; j < 3; j++)
            {
                OldWeigths[i][j] = new float[HalfKav2Format[j], HalfKav2Format[j + 1]];
                WeigthChanges[i][j] = new float[HalfKav2Format[j], HalfKav2Format[j + 1]];
                Weigths[i][j] = new float[HalfKav2Format[j], HalfKav2Format[j + 1]];
                for (int k = 0; k < Weigths[i][j].GetLength(0); k++)
                {
                    for (int l = 0; l < Weigths[i][j].GetLength(1); l++)
                    {
                        Weigths[i][j][k, l] = Convert.ToSingle(((float)random.NextDouble() - 0.5) * 1 / 2);
                    }
                }
            }
        }
        //init HalfKp Weights
        for (int i = 0; i < HalfkpWeigths.Length; i++)
        {
            HalfkpWeigth_momentum[i] = new double[HalfkpFormat[i], HalfkpFormat[i + 1]];
            HalfkpWeigth_old_momentum[i] = new double[HalfkpFormat[i], HalfkpFormat[i + 1]];
            HalfkpWeigth_velocity[i] = new double[HalfkpFormat[i], HalfkpFormat[i + 1]];
            HalfkpWeigth_velocity_max[i] = new double[HalfkpFormat[i], HalfkpFormat[i + 1]];
            HalfkpWeigth_lookahead[i] = new double[HalfkpFormat[i], HalfkpFormat[i + 1]];
            HalfkpWeigthChanges[i] = new double[HalfkpFormat[i], HalfkpFormat[i + 1]];
            HalfkpWeigths[i] = new double[HalfkpFormat[i], HalfkpFormat[i + 1]];
            for (int j = 0; j < HalfkpWeigths[i].GetLength(0); j++)
            {
                for (int k = 0; k < HalfkpWeigths[i].GetLength(1); k++)
                {
                    HalfkpWeigths[i][j,k] = Convert.ToSingle(((float)random.NextDouble() - 0.5) * 1 / 2);
                }
            }
        }
        //Init start Matrix
        for (int i = 0; i < StartMatrix.GetLength(0); i++)
        {
            //HalfKav2
            for (int j = 0; j < StartMatrix.GetLength(1); j++)
            {
                StartMatrix[i, j] = Convert.ToSingle(((float)random.NextDouble() - 0.5) * 1 / 2);
            }
        }
        for (int i = 0; i < HalfkpMatrix.GetLength(0); i++)
        {
            //HalfKp
            for (int j = 0; j < HalfkpMatrix.GetLength(1); j++)
            {
                HalfkpMatrix[i, j] = Convert.ToSingle(((float)random.NextDouble() - 0.5) * 1 / 2);
            }
        }
        //Init HalfKp Matrix Bias
        for(int i = 0; i < HalfkpMatrixBias.Length; i++)
            HalfkpMatrixBias[i] = Convert.ToSingle(((float)random.NextDouble() - 0.5) * 1 / 2);
        PSQTInit();
    }
    public float[] StartCompleteMatrixMultiply(List<int> InputVector)
    {
        float[] Output = new float[520];
        Piececount = 1;

        foreach (int Place in InputVector)
        {
            if (Place > -1)
            {
                Piececount++;
                for (int j = 0; j < 520; j++)
                    Output[j] += StartMatrix[Place, j];
            }
        }
        return Output;
    }
    public List<int> ConvertVectorToHalfkp(List<int> InputVector)
    {
        for (int i = 0; i < InputVector.Count; i++)
        {
            int Place = InputVector[i];
            if (Place > -1 && (Place < 20480 || Place > 24575))
            {
                if (Place > 24575)
                    Place -= 4096;
            }
            else
                Place = -1;
            InputVector[i] = Place;
        }
        return InputVector;
    }
    public double[] HalfKpMatrixMultiply(List<int>InputVector)
    {
        double[] Output = new double[256];
        for (int i = 0; i < 256; i++)
            Output[i] = HalfkpMatrixBias[i];
        
        InputVector = ConvertVectorToHalfkp(InputVector);
        foreach (int Place in InputVector)
            if (Place > -1)
                for (int j = 0; j < 256; j++)
                    Output[j] += HalfkpMatrix[Place, j];
        return Output;
    }
    public void printLayerOutput(float[] input_vector)
    {
        string Output = "";
        int counter = 0;
        foreach (float Value in input_vector)
        {
            Output += Math.Round(Value, 2) + " ";
            counter++;
            if (counter == 256)
                Output += "\n256: \n";
        }
        Console.WriteLine("\nThe vector values are:\n {0}", Output);
    }
    public double HalfkpEval(double[] Input)
    {
        for (int i = 0; i < 3; i++)
        {
            //Layer
            for (int j = 0; j < HalfkpWeigths[i].GetLength(1); j++)
            {
                HalfkpNeuronInput[i][j] = 0;
                //Neuron
                if (i == 0)
                    for (int k = 0; k < HalfkpWeigths[i].GetLength(0); k++)
                        HalfkpNeuronInput[i][j] += HalfkpWeigths[i][k, j] * Input[k];
                else
                    for (int k = 0; k < HalfkpWeigths[i].GetLength(0); k++)
                        HalfkpNeuronInput[i][j] += HalfkpWeigths[i][k, j] * HalfkpNeuronVal[i - 1][k];
                //Add Bias
                HalfkpNeuronInput[i][j] += HalfkpBiases[i][j];
                //Input Connections in Neuron
                HalfkpNeuronVal[i][j] = ClippedReLU(HalfkpNeuronInput[i][j]);
            }
        }
        return HalfkpNeuronInput[2][0];
    }
    public double[] NetCost(TrainingPosition[] Input , bool NetType)
    {
        if (NetType)
            return CostOfNet(Input);
        else
            return CostOfHalfkpNet(Input);
    }
    public double UseHalfkpNet(byte[,] InputBoard, byte color)
    {
        List<int>[] Input = BoardToHalfKav2(InputBoard, color);

        double[] EvalVector = new double[512], ConverterOur = new double[256], ConverterTheir = new double[256];
        ConverterOur = HalfKpMatrixMultiply(Input[0]);
        ConverterTheir = HalfKpMatrixMultiply(Input[1]);
        for (int i = 0; i < 256; i++)
        {
            EvalVector[i] = ConverterOur[i];
            EvalVector[i + 256] = ConverterTheir[i];
        }
        for (int i = 0; i < 512; i++)
            EvalVector[i] = ClippedReLU(EvalVector[i]);
        Array.Copy(EvalVector, HalfkpMatrixOut, 512);
        HalfkpOutNet = HalfkpEval(EvalVector);
        // Return the Output as a Relu
        return HalfkpOutNet;
    }
    public void PSQTInit()
    {
        PSQT PieceSquareTables = new PSQT();
        //PieceType
        for (int l = 0; l < 12; l++)
        {
            //KingX
            for (int j = 1; j < 9; j++)
            {
                //KingY
                for (int z = 1; z < 9; z++)
                {
                    //X
                    for (int i = 1; i < 9; i++)
                    {
                        //Y
                        for (int k = 1; k < 9; k++)
                        {
                            switch (l)
                            {
                                case 7:
                                    for (float y = 0; y < 8; y++)
                                        StartMatrix[GeneratePlaceFromBoard(i, k, j, z, l, 1), (int)y] = -((PieceSquareTables.PawnMG[-i+9, k] / 100 + 0.82f) * y + (PieceSquareTables.PawnEG[-i + 9, k] / 100 + 0.94f) * (7 - y)) / 7;
                                    break;
                                case 8:
                                    for (float y = 0; y < 8; y++)
                                        StartMatrix[GeneratePlaceFromBoard(i, k, j, z, l, 1), (int)y] = -((PieceSquareTables.KnightMG[-i + 9, k] / 100 + 3.37f) * y + (PieceSquareTables.KnightEG[-i + 9, k] / 100 + 2.81f) * (7 - y)) / 7;
                                    break;
                                case 9:
                                    for (float y = 0; y < 8; y++)
                                        StartMatrix[GeneratePlaceFromBoard(i, k, j, z, l, 1), (int)y] = -((PieceSquareTables.BishopMG[-i + 9, k] / 100 + 3.65f) * y + (PieceSquareTables.BishopMG[-i + 9, k] / 100 + 2.97f) * (7 - y)) / 7;
                                    break;
                                case 6:
                                    for (float y = 0; y < 8; y++)
                                        StartMatrix[GeneratePlaceFromBoard(i, k, j, z, l, 1), (int)y] = -((PieceSquareTables.KingMG[-i + 9, k] / 100) * y + (PieceSquareTables.KingEG[-i + 9, k] / 100) * (7 - y)) / 7;
                                    break;
                                case 10:
                                    for (float y = 0; y < 8; y++)
                                        StartMatrix[GeneratePlaceFromBoard(i, k, j, z, l, 1), (int)y] = -((PieceSquareTables.QueenMG[-i + 9, k] / 100 + 10.25f) * y + (PieceSquareTables.QueenMG[-i + 9, k] / 100 + 9.36f) * (7 - y)) / 7;
                                    break;
                                case 11:
                                    for (float y = 0; y < 8; y++)
                                        StartMatrix[GeneratePlaceFromBoard(i, k, j, z, l, 1), (int)y] = -((PieceSquareTables.RookMG[-i + 9, k] / 100 + 4.77f) * y + (PieceSquareTables.RookMG[-i + 9, k] / 100 + 5.12f) * (7 - y)) / 7;
                                    break;
                                case 1:
                                    for (float y = 0; y < 8; y++)
                                        StartMatrix[GeneratePlaceFromBoard(i, k, j, z, l, 1), (int)y] = ((PieceSquareTables.PawnMG[-i + 9, k] / 100 + 0.82f) * y + (PieceSquareTables.PawnEG[-i + 9, k ] / 100 + 0.94f) * (7 - y)) / 7;
                                    break;
                                case 2:
                                    for (float y = 0; y < 8; y++)
                                        StartMatrix[GeneratePlaceFromBoard(i, k, j, z, l, 1), (int)y] = ((PieceSquareTables.KnightMG[-i + 9, k] / 100 + 3.37f) * y + (PieceSquareTables.KnightEG[-i + 9, k ] / 100 + 2.81f) * (7 - y)) / 7;
                                    break;
                                case 3:
                                    for (float y = 0; y < 8; y++)
                                        StartMatrix[GeneratePlaceFromBoard(i, k, j, z, l, 1), (int)y] = ((PieceSquareTables.BishopMG[-i + 9, k] / 100 + 3.65f) * y + (PieceSquareTables.BishopMG[-i + 9, k] / 100 + 2.97f) * (7 - y)) / 7;
                                    break;
                                case 4:
                                    for (float y = 0; y < 8; y++)
                                        StartMatrix[GeneratePlaceFromBoard(i, k, j, z, l, 1), (int)y] = ((PieceSquareTables.QueenMG[-i + 9, k] / 100 + 10.25f) * y + (PieceSquareTables.QueenMG[-i + 9, k] / 100 + 9.36f) * (7 - y)) / 7;
                                    break;
                                case 5:
                                    for (float y = 0; y < 8; y++)
                                        StartMatrix[GeneratePlaceFromBoard(i, k, j, z, l, 1), (int)y] = ((PieceSquareTables.RookMG[-i + 9, k] / 100 + 4.77f) * y + (PieceSquareTables.RookMG[-i + 9, k] / 100 + 5.12f) * (7 - y)) / 7;
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
            }
        }
    }
    public double ReluDerivative(double Input)
    {
        if (Input == 0 || Input == 1)
            return 0;
        else
            return 1;
    }
    public double GetPerspective(int Index, float[] Our, float[] Their)
    {
        int Place = Index;
        PerspectiveOur = Our[Place];
        PerspectiveTheir = Their[Place];
        return BigClippedRelu((PerspectiveOur - PerspectiveTheir) / 2);
    }
    public double BigClippedRelu(double Input)
    {
        if (Input > 1)
            return 1;
        else if (Input < -1)
            return -1;
        else return Input;
    }
    public int GetPlace_0_To_7_()
    {
        return (Piececount - 1) / 4;
    }
    public double ClippedReLU(double Input)
    {
        if (Input < 0)
            return 0;
        else if (Input > 1)
            return 1;
        else
            return Input;
    }
    public float UseEvalNet(int Net, float[] Input)
    {
        for (int i = 0; i < 3; i++)
        {
            //Layer
            for (int j = 0; j < Weigths[Net][i].GetLength(1); j++)
            {
                NeuronInput[i][j] = 0;
                //Neuron
                if (i == 0)
                    for (int k = 0; k < Weigths[Net][i].GetLength(0); k++)
                        NeuronInput[i][j] += Weigths[Net][i][k, j] * Input[k];
                else
                    for (int k = 0; k < Weigths[Net][i].GetLength(0); k++)
                        NeuronInput[i][j] += Weigths[Net][i][k, j] * NeuronVal[i - 1][k];
                //Add Bias
                NeuronInput[i][j] += Biases[Net][i][j];
                //Input Connections in Neuron
                NeuronVal[i][j] = (float)ClippedReLU(NeuronInput[i][j]);
            }
        }
        return NeuronInput[2][0];
    }
    public float UseNet(byte[,] InputBoard, byte color)
    {
        List<int>[] Input = BoardToHalfKav2(InputBoard, color);
        float[] OurVector = new float[8], TheirVector = new float[8], EvalVector = new float[1024];
        float[] ConverterOur = new float[520], ConverterTheir = new float[520];
        ConverterOur = StartCompleteMatrixMultiply(Input[0]);
        ConverterTheir = StartCompleteMatrixMultiply(Input[1]);
        for (int i = 0; i < 8; i++)
        {
            OurVector[i] = ConverterOur[i];
            TheirVector[i] = ConverterTheir[i];
        }
        for (int i = 8; i < 520; i++)
        {
            EvalVector[i - 8] = ConverterOur[i];
            EvalVector[i + 504] = ConverterTheir[i];
        }
        Array.Copy(EvalVector, MatrixOut, 1024);
        OutNet = UseEvalNet(GetPlace_0_To_7_(), EvalVector);
        OutMatrix = (float)GetPerspective(GetPlace_0_To_7_(), OurVector, TheirVector);
        // Return the Output as a Relu
        return OutMatrix + OutNet;
    }
    public float[,] ReturnNetOutputs(List<int>[] Input)
    {
        float[] OurVector = new float[8], TheirVector = new float[8], EvalVector = new float[1024];
        float[] ConverterOur = new float[520], ConverterTheir = new float[520];
        float[,] Output = new float[2, 8];
        ConverterOur = StartCompleteMatrixMultiply(Input[0]);
        ConverterTheir = StartCompleteMatrixMultiply(Input[1]);
        for (int i = 0; i < 8; i++)
        {
            OurVector[i] = ConverterOur[i];
            TheirVector[i] = ConverterTheir[i];
        }
        for (int i = 8; i < 520; i++)
        {
            EvalVector[i - 8] = ConverterOur[i];
            EvalVector[i + 504] = ConverterTheir[i];
        }
        MatrixOut = EvalVector;
        for (int i = 0; i < 8; i++)
        {
            Output[1, i] = UseEvalNet(i, EvalVector);
            Output[0, i] = (float)GetPerspective(i, OurVector, TheirVector);
        }
        return Output;
    }
    public void SaveNet(string FileName, bool UseBackup)
    {
        StreamWriter sw = new StreamWriter(FileName, false, System.Text.Encoding.UTF8);
        StreamWriter swBackup = new StreamWriter("Nothing");
        if (UseBackup)
            swBackup = new StreamWriter("Backup.nnue", false, System.Text.Encoding.UTF8);
        bool start = true;
        string FileContent = "1 ";
        //Weights
        for (int i = 0; i < Weigths.Length; i++)
        {
            for (int j = 0; j < Weigths[i].Length; j++)
            {
                for (int k = 0; k < Weigths[i][j].GetLength(0); k++)
                {
                    if (UseBackup)
                    {
                        for (int l = 0; l < Weigths[i][j].GetLength(1); l++)
                            FileContent += Weigths[i][j][k, l] + " ";
                        swBackup.Write(FileContent);
                        FileContent = "";
                        if (start)
                        {
                            FileContent = "1 ";
                            start = false;
                        }
                    }
                    for (int l = 0; l < Weigths[i][j].GetLength(1); l++)
                        FileContent += BitConverter.SingleToInt32Bits(Weigths[i][j][k, l]) + " ";
                    sw.Write(FileContent);
                    FileContent = "";
                }
            }
        }
        //Biases
        for (int i = 0; i < Biases.Length; i++)
        {
            for (int j = 0; j < Biases[i].Length; j++)
            {
                if (UseBackup)
                {
                    for (int k = 0; k < Biases[i][j].Length; k++)
                        FileContent += Biases[i][j][k] + " ";
                    swBackup.Write(FileContent);
                    FileContent = "";
                }
                for (int k = 0; k < Biases[i][j].Length; k++)
                    FileContent += BitConverter.SingleToInt32Bits(Biases[i][j][k]) + " ";
                sw.Write(FileContent);
                FileContent = "";
            }
        }
        //StartMatrix
        for (int i = 0; i < StartMatrix.GetLength(0); i++)
        {
            if (UseBackup)
            {
                for (int j = 0; j < StartMatrix.GetLength(1); j++)
                    FileContent += StartMatrix[i, j] + " ";
                swBackup.Write(FileContent);
                FileContent = "";
            }
            for (int j = 0; j < StartMatrix.GetLength(1); j++)
                FileContent += BitConverter.SingleToInt32Bits(StartMatrix[i, j]) + " ";
            sw.Write(FileContent);
            FileContent = "";
        }

        sw.Write(FileContent);

        sw.Flush();
        sw.Close();
        swBackup.Flush();
        swBackup.Close();
        Console.WriteLine("Done !");
    }
    public bool DetectNetType(string FileName)
    {
        StreamReader sr = new StreamReader(FileName);
        string start = Convert.ToString(sr.Peek() - 48);

        if (start == "0")
            return false;
        else
            return true;
    }
    public void LoadNet(string FileName , bool WriteType)
    {
        if (WriteType)
        {
            if (!DetectNetType(FileName))
                Console.WriteLine("HalfKp Net Detected !");
            else
                Console.WriteLine("HalfKav2 Net Detected !"); 
        }
        if (!DetectNetType(FileName))
            OpenHalfkpNet(FileName, WriteType);
        else
            OpenNet(FileName);
    }
    public void SaveNets(string FileName , bool NetType , bool Backup)
    {
        if (NetType)
            SaveNet(FileName, Backup);
        else
            SaveHalfkpNet(FileName, Backup);
    }
    public void SaveHalfkpNet(string FileName, bool UseBackup)
    {
        StreamWriter sw = new StreamWriter(FileName, false, System.Text.Encoding.UTF8);
        StreamWriter swBackup = new StreamWriter("Nothing");
        if (UseBackup)
            swBackup = new StreamWriter("Backup.nnue", false, System.Text.Encoding.UTF8);
        bool start = true;
        string FileContent = "0 ";
        //Weights
        for (int j = 0; j < HalfkpWeigths.Length; j++)
        {
            for (int k = 0; k < HalfkpWeigths[j].GetLength(0); k++)
            {
                if (UseBackup)
                {
                    for (int l = 0; l < HalfkpWeigths[j].GetLength(1); l++)
                        FileContent += HalfkpWeigths[j][k, l] + " ";
                    swBackup.Write(FileContent);
                    FileContent = "";
                    if(start)
                    {
                        FileContent = "0 ";
                        start = false;
                    }
                }
                for (int l = 0; l < HalfkpWeigths[j].GetLength(1); l++)
                    FileContent += BitConverter.SingleToInt32Bits((float)HalfkpWeigths[j][k, l]) + " ";
                sw.Write(FileContent);
                FileContent = "";
            }
        }
        //Biases
        for (int j = 0; j < HalfkpBiases.Length; j++)
        {
            if (UseBackup)
            {
                for (int k = 0; k < HalfkpBiases[j].Length; k++)
                    FileContent += HalfkpBiases[j][k] + " ";
                swBackup.Write(FileContent);
                FileContent = "";
            }
            for (int k = 0; k < HalfkpBiases[j].Length; k++)
                FileContent += BitConverter.SingleToInt32Bits((float)HalfkpBiases[j][k]) + " ";
            sw.Write(FileContent);
            FileContent = "";
        }
        //StartMatrix
        for (int i = 0; i < HalfkpMatrix.GetLength(0); i++)
        {
            if (UseBackup)
            {
                for (int j = 0; j < HalfkpMatrix.GetLength(1); j++)
                    FileContent += HalfkpMatrix[i, j] + " ";
                swBackup.Write(FileContent);
                FileContent = "";
            }
            for (int j = 0; j < HalfkpMatrix.GetLength(1); j++)
                FileContent += BitConverter.SingleToInt32Bits((float)HalfkpMatrix[i, j]) + " ";
            sw.Write(FileContent);
            FileContent = "";
        }
        //StartMatrixBias

        if (UseBackup)
        {
            for (int j = 0; j < HalfkpMatrixBias.Length; j++)
                FileContent += HalfkpMatrixBias[j] + " ";
            swBackup.Write(FileContent);
            FileContent = "";
        }
        for (int j = 0; j < HalfkpMatrixBias.Length; j++)
            FileContent += BitConverter.SingleToInt32Bits((float)HalfkpMatrixBias[j]) + " ";
        sw.Write(FileContent);
        FileContent = "";
        sw.Write(FileContent);

        sw.Flush();
        sw.Close();
        swBackup.Flush();
        swBackup.Close();
        Console.WriteLine("Done !");
    }
    public void OpenHalfkpNet(string FileName , bool showDialog)
    {
        int counter = 1;
        StreamReader sr = new StreamReader(FileName);
        string FileOutput = sr.ReadToEnd();
        string[] Values;
        sr.Close();

        Values = FileOutput.Split(' ');

        //Weights
        for (int j = 0; j < HalfkpWeigths.Length; j++)
        {
            for (int k = 0; k < HalfkpWeigths[j].GetLength(0); k++)
            {
                for (int l = 0; l < HalfkpWeigths[j].GetLength(1); l++)
                {
                    HalfkpWeigths[j][k, l] = BitConverter.Int32BitsToSingle(Convert.ToInt32(Values[counter]));
                    HalfkpWeigth_lookahead[j][k, l] = HalfkpWeigths[j][k, l];
                    counter++;
                }
            }
        }

        //Biases
        for (int j = 0; j < HalfkpBiases.Length; j++)
        {
            for (int k = 0; k < HalfkpBiases[j].Length; k++)
            {
                HalfkpBiases[j][k] = BitConverter.Int32BitsToSingle(Convert.ToInt32(Values[counter]));
                HalfkpBias_lookahead[j][k] = HalfkpBiases[j][k];
                counter++;
            }
        }

        //StartMatrix
        for (int i = 0; i < HalfkpMatrix.GetLength(0); i++)
        {
            for (int j = 0; j < HalfkpMatrix.GetLength(1); j++)
            {
                HalfkpMatrix[i, j] = BitConverter.Int32BitsToSingle(Convert.ToInt32(Values[counter]));
                HalfkpMatrix_lookahead[i, j] = HalfkpMatrix[i, j];
                counter++;
            }
        }

        //StartMatrixBias
        for (int j = 0; j < HalfkpMatrixBias.Length; j++) 
        {
            HalfkpMatrixBias[j] = BitConverter.Int32BitsToSingle(Convert.ToInt32(Values[counter]));
            HalfkpMatrixBias_lookahead[j] = HalfkpMatrixBias[j];
            counter++;
        }
        if (showDialog)
            Console.WriteLine("Done !");
    }
    public void OpenNet(string FileName)
    {
        int counter = 1;
        StreamReader sr = new StreamReader(FileName);
        string FileOutput = sr.ReadToEnd();
        string[] Values;
        sr.Close();

        Values = FileOutput.Split(' ');
        //Weights
        for (int i = 0; i < Weigths.Length; i++)
        {
            for (int j = 0; j < Weigths[i].Length; j++)
            {
                for (int k = 0; k < Weigths[i][j].GetLength(0); k++)
                {
                    for (int l = 0; l < Weigths[i][j].GetLength(1); l++)
                    {
                        Weigths[i][j][k, l] = BitConverter.Int32BitsToSingle(Convert.ToInt32(Values[counter]));
                        counter++;
                    }
                }
            }
        }

        //Biases
        for (int i = 0; i < Biases.Length; i++)
        {
            for (int j = 0; j < Biases[i].Length; j++)
            {
                for (int k = 0; k < Biases[i][j].Length; k++)
                {
                    Biases[i][j][k] = BitConverter.Int32BitsToSingle(Convert.ToInt32(Values[counter]));
                    counter++;
                }
            }
        }

        //StartMatrix
        for (int i = 0; i < StartMatrix.GetLength(0); i++)
        {
            for (int j = 0; j < StartMatrix.GetLength(1); j++)
            {
                StartMatrix[i, j] = BitConverter.Int32BitsToSingle(Convert.ToInt32(Values[counter]));
                counter++;
            }
        }
        Console.WriteLine("Done !");
    }
    public List<int>[] BoardToHalfKav2(byte[,] InputBoard, byte color)
    {
        int KingWX = 0, KingWY = 0, KingBX = 0, KingBY = 0;
        List<int>[] Indexes = new List<int>[2];
        int CurrentKingPlaceX, CurrentKingPlaceY;
        int view = 0;
        //the view is always from the adversary (the person not to play)
        if (view == color)
            view = 1;
        //Find the King Positions
        for (int i = 1; i < 9; i++)
        {
            for (int j = 1; j < 9; j++)
            {
                if ((InputBoard[i, j] - (InputBoard[i, j] >> 4) * 0b10000) >> 1 == 0b11)
                {
                    if ((InputBoard[i, j] >> 4) == 1)
                    {
                        KingWX = i;
                        KingWY = j;
                    }
                    else
                    {
                        KingBX = i;
                        KingBY = j;
                    }
                }
            }
        }
        //Create the nnue Vectors  
        for (int i = 0; i < 2; i++)
        {
            Indexes[i] = new List<int>();
            if (view == 0)
            {
                CurrentKingPlaceX = KingBX;
                CurrentKingPlaceY = KingBY;
            }
            else
            {
                CurrentKingPlaceX = KingWX;
                CurrentKingPlaceY = KingWY;
            }
            //Compute all the positions that are one
            for (int j = 1; j < 9; j++)
            {
                for (int k = 1; k < 9; k++)
                {
                    if (InputBoard[j, k] != 0)
                    {
                        switch (InputBoard[j, k])
                        {
                            //Pawn
                            case 0b00000001:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 7, view));
                                break;
                                //Pawn
                            case 0b00000010:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 7, view));
                                break;
                                //Pawn
                            case 0b00000011:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 7, view));
                                break;
                                //Knight
                            case 0b00000100:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 8, view));
                                break;
                                //Bishop
                            case 0b00000101:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 9, view));
                                break;
                                //King
                            case 0b00000110:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 6, view));
                                break;
                                //King
                            case 0b00000111:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 6, view));
                                break;
                                //Queen
                            case 0b00001000:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 10, view));
                                break;
                                //Rook
                            case 0b00001001:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 11, view));
                                break;
                                //Rook
                            case 0b00001010:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 11, view));
                                break;

                            case 0b00010001:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 1, view));
                                break;
                            case 0b00010010:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 1, view));
                                break;
                            case 0b00010011:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 1, view));
                                break;
                            case 0b00010100:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 2, view));
                                break;
                            case 0b00010101:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 3, view));
                                break;
                            case 0b00010110:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 0, view));
                                break;
                            case 0b00010111:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 0, view));
                                break;
                            case 0b00011000:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 4, view));
                                break;
                            case 0b00011001:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 5, view));
                                break;
                            case 0b00011010:
                                Indexes[i].Add(GeneratePlaceFromBoard(j, k, CurrentKingPlaceX, CurrentKingPlaceY, 5, view));
                                break;
                        }
                    }
                }
            }
            if (view == 1)
                view = 0;
            else
                view = 1;
        }
        return Indexes;
    }
    public PSQTValue[,] PSQTOUtput(byte[,] InputBoard, byte color)
    {
        PSQTValue[,] BoardOutput = new PSQTValue[9, 9];
        int KingWX = 0, KingWY = 0, KingBX = 0, KingBY = 0;
        int counter = -1;
        int view = 0;
        //the view is always from the adversary (the person not to play)
        if (view == color)
            view = 1;
        //Find the King Positions
        for (int i = 1; i < 9; i++)
        {
            for (int j = 1; j < 9; j++)
            {
                if ((InputBoard[i, j] - (InputBoard[i, j] >> 4) * 0b10000) >> 1 == 0b11)
                {
                    if ((InputBoard[i, j] >> 4) == 1)
                    {
                        KingWX = i;
                        KingWY = j;
                    }
                    else
                    {
                        KingBX = i;
                        KingBY = j;
                    }
                }
                if(InputBoard[i,j] != 0)
                    counter++;
            }
        }
        for (int j = 1; j < 9; j++)
        {
            for (int k = 1; k < 9; k++)
            {
                if (InputBoard[j, k] != 0)
                {
                    switch (InputBoard[j, k])
                    {
                        case 0b00000001:
                            BoardOutput[j,k] = new PSQTValue();
                            BoardOutput[j, k].Value = ReturnEval(j, k, KingBX, KingBY, KingWX, KingWY, 7, view, counter / 8);
                            BoardOutput[j, k].Name = "p";
                            break;
                        case 0b00000010:
                            BoardOutput[j, k] = new PSQTValue();
                            BoardOutput[j, k].Value = ReturnEval(j, k, KingBX, KingBY, KingWX, KingWY, 7, view, counter / 8);
                            BoardOutput[j, k].Name = "p";
                            break;
                        case 0b00000011:
                            BoardOutput[j, k] = new PSQTValue();
                            BoardOutput[j, k].Value = ReturnEval(j, k, KingBX, KingBY, KingWX, KingWY, 7, view, counter / 8);
                            BoardOutput[j, k].Name = "p";
                            break;
                        case 0b00000100:
                            BoardOutput[j, k] = new PSQTValue();
                            BoardOutput[j, k].Value = ReturnEval(j, k, KingBX, KingBY, KingWX, KingWY, 8, view, counter / 8);
                            BoardOutput[j, k].Name = "n";
                            break;
                        case 0b00000101:
                            BoardOutput[j, k] = new PSQTValue();
                            BoardOutput[j, k].Value = ReturnEval(j, k, KingBX, KingBY, KingWX, KingWY, 9, view, counter / 8);
                            BoardOutput[j, k].Name = "b";
                            break;
                        case 0b00000110:
                            BoardOutput[j, k] = new PSQTValue();
                            BoardOutput[j, k].Name = "k";
                            break;
                        case 0b00000111:
                            BoardOutput[j, k] = new PSQTValue();
                            BoardOutput[j, k].Name = "k";
                            break;
                        case 0b00001000:
                            BoardOutput[j, k] = new PSQTValue();
                            BoardOutput[j, k].Value = ReturnEval(j, k, KingBX, KingBY, KingWX, KingWY, 10, view, counter / 8);
                            BoardOutput[j, k].Name = "q";
                            break;
                        case 0b00001001:
                            BoardOutput[j, k] = new PSQTValue();
                            BoardOutput[j, k].Value = ReturnEval(j, k, KingBX, KingBY, KingWX, KingWY, 11, view, counter / 8);
                            BoardOutput[j, k].Name = "r";
                            break;
                        case 0b00001010:
                            BoardOutput[j, k] = new PSQTValue();
                            BoardOutput[j, k].Value = ReturnEval(j, k, KingBX, KingBY, KingWX, KingWY, 11, view, counter / 8);
                            BoardOutput[j, k].Name = "r";
                            break;

                        case 0b00010001:
                            BoardOutput[j, k] = new PSQTValue();
                            BoardOutput[j, k].Value = ReturnEval(j, k, KingBX, KingBY, KingWX, KingWY, 1, view, counter / 8);
                            BoardOutput[j, k].Name = "P";
                            break;
                        case 0b00010010:
                            BoardOutput[j, k] = new PSQTValue();
                            BoardOutput[j, k].Value = ReturnEval(j, k, KingBX, KingBY, KingWX, KingWY, 1, view, counter / 8);
                            BoardOutput[j, k].Name = "P";
                            break;
                        case 0b00010011:
                            BoardOutput[j, k] = new PSQTValue();
                            BoardOutput[j, k].Value = ReturnEval(j, k, KingBX, KingBY, KingWX, KingWY, 1, view, counter / 8);
                            BoardOutput[j, k].Name = "P";
                            break;
                        case 0b00010100:
                            BoardOutput[j, k] = new PSQTValue();
                            BoardOutput[j, k].Value = ReturnEval(j, k, KingBX, KingBY, KingWX, KingWY, 2, view, counter / 8);
                            BoardOutput[j, k].Name = "N";
                            break;
                        case 0b00010101:
                            BoardOutput[j, k] = new PSQTValue();
                            BoardOutput[j, k].Value = ReturnEval(j, k, KingBX, KingBY, KingWX, KingWY, 3, view, counter / 8);
                            BoardOutput[j, k].Name = "B";
                            break;
                        case 0b00010110:
                            BoardOutput[j, k] = new PSQTValue();
                            BoardOutput[j, k].Name = "K";
                            break;
                        case 0b00010111:
                            BoardOutput[j, k] = new PSQTValue();
                            BoardOutput[j, k].Name = "K";
                            break;
                        case 0b00011000:
                            BoardOutput[j, k] = new PSQTValue();
                            BoardOutput[j, k].Value = ReturnEval(j, k, KingBX, KingBY, KingWX, KingWY, 4, view, counter / 8);
                            BoardOutput[j, k].Name = "Q";
                            break;
                        case 0b00011001:
                            BoardOutput[j, k] = new PSQTValue();
                            BoardOutput[j, k].Value = ReturnEval(j, k, KingBX, KingBY, KingWX, KingWY, 5, view, counter / 8);
                            BoardOutput[j, k].Name = "R";
                            break;
                        case 0b00011010:
                            BoardOutput[j, k] = new PSQTValue();
                            BoardOutput[j, k].Value = ReturnEval(j, k, KingBX, KingBY, KingWX, KingWY, 5, view, counter / 8);
                            BoardOutput[j, k].Name = "R";
                            break;
                    }
                }
            }
        }
        return BoardOutput;
    }
    public float ReturnEval(int X , int Y , int BlackKingX , int BlackKingY , int WhiteKingX , int WhiteKingY , int PieceType , int Color , int Place)
    {
        float Value = 0;
        int CurrentKingX, CurrentKingY;
        int Multiplicator = 1;
        for (int i = 0; i < 1; i++)
        {
            if (Color == 1)
                Color = 0;
            else
                Color = 1;
            if(Color == 0)
            {
                CurrentKingX = BlackKingX;
                CurrentKingY = BlackKingY;
            }
            else
            {
                CurrentKingX = WhiteKingX;
                CurrentKingY = WhiteKingY;
            }
            int place = GeneratePlaceFromBoard(X, Y, CurrentKingX, CurrentKingY, PieceType, Color);
            if (place > 0)
                Value += Multiplicator * StartMatrix[place, Place];
            else
                return float.NaN;
            Multiplicator = -1;
        }
        return Value;
    }
    public int GeneratePlaceFromBoard(int x, int y, int KingX, int KingY, int Piecetype, int color)
    {
        if (color == 0)
        {
            y = -y + 9;
            KingY = -KingY + 9;
        }
        int PositionPiece = (y - 1) * 8 + x - 1;
        int PositionKing = (KingY - 1) * 8 + KingX -1;
        if (color == 0)
        {
            //Black Piece
            if (Piecetype - 5 > 0)
                Piecetype -= 6;
            else
                Piecetype += 6;
        }
        return ((Piecetype - 1) * 4096 + PositionKing * 64 + PositionPiece);
    }
    public double[] CostOfNet(TrainingPosition[] Input)
    {
        Classic_Eval eval = new Classic_Eval();
        double Cost = 0 , StaticEvalCost = 0;
        foreach (TrainingPosition TrainingExample in Input)
        {
            double color = TrainingExample.Color;
            double Output = UseNet(TrainingExample.Board, (byte)color);
            double Value = TrainingExample.Eval;
            double CurrentCost = (Value - Output);
            if (CurrentCost < 0)
                CurrentCost = -CurrentCost;

            Cost += CurrentCost;
            Output = eval.PestoEval(TrainingExample.Board, (byte)color);
            CurrentCost = (Value - Output);
            if (CurrentCost < 0)
                CurrentCost = -CurrentCost;

            StaticEvalCost += CurrentCost;

        }
        return new double[2] { Cost / Input.Length, StaticEvalCost / Input.Length };
    }
    public double LargeSigmoid(float Input , float Size)
    {
        return (Input/ Size) / (float)Math.Sqrt((Input / Size) * (Input / Size) + 1);
    }
    public double[] CostOfHalfkpNet(TrainingPosition[] Input)
    {
        Classic_Eval eval = new Classic_Eval();
        double Cost = 0, StaticEvalCost = 0, smallest_cost = 1, largest_cost = 0;
        foreach (TrainingPosition TrainingExample in Input)
        {
            double color = TrainingExample.Color;
            double Output = UseHalfkpNet(TrainingExample.Board, (byte)color);
            double Value = TrainingExample.Eval;
            double CurrentCost = (Value - Output);
            if (CurrentCost < 0)
                CurrentCost = -CurrentCost;
            if (CurrentCost > largest_cost)
                largest_cost = CurrentCost;
            else if (CurrentCost < smallest_cost)
                smallest_cost = CurrentCost;
            Cost += CurrentCost;

            Cost += CurrentCost;
            Output = eval.PestoEval(TrainingExample.Board, (byte)color);
            CurrentCost = (Value - Output);
            if (CurrentCost < 0)
                CurrentCost = -CurrentCost;

            StaticEvalCost += CurrentCost;
        }
        Console.WriteLine("smallest cost {0}", smallest_cost);
        Console.WriteLine("largest cost {0}", largest_cost);
        return new double[2] { Cost / Input.Length, StaticEvalCost / Input.Length };
    }
    public void BackPropagation2(TrainingPosition[] TrainingInput, float LearningRate)
    {
        //init Connection deltas
        float[] MatrixErrorA = new float[520], MatrixErrorB = new float[520];
        float Lernrate = LearningRate;
        float LastNeuronOut = 0;

        // init BiasChanges
        for (int i = 0; i < Biases.Length; i++)
        {
            BiasChange[i] = new float[3][];
            for (int j = 0; j < Biases[i].Length; j++)
                BiasChange[i][j] = new float[HalfKav2Format[j + 1]];
        }
        // init WeightChanges
        for (int i = 0; i < Weigths.Length; i++)
        {
            WeigthChanges[i] = new float[3][,];
            for (int j = 0; j < 3; j++)
                WeigthChanges[i][j] = new float[HalfKav2Format[j], HalfKav2Format[j + 1]];
        }
        //Init start MatrixChanges
        MatrixChange = new float[45056, 520];

        for (int gen = 0; gen < TrainingInput.Length; gen++)
        {
            float Value = TrainingInput[gen].Eval;
            float color = TrainingInput[gen].Color;
            List<int>[] Input = BoardToHalfKav2(TrainingInput[gen].Board, (byte)color);
            
            float NetOutput = UseNet(TrainingInput[gen].Board, (byte)color);
            if (NetOutput != Value)
            {
                //ResetConnectionErrorDeltas
                MatrixErrorA = new float[520];
                MatrixErrorB = new float[520];

                int Nettype = GetPlace_0_To_7_();
                float Error = 2 * (NetOutput - Value);
                float OutError = Error - (PerspectiveOur - PerspectiveTheir) / 2;
                NeuronErrors[2][0] = OutError;
                for (int i = NeuronVal.Length - 1; i > -1; i--)
                {
                    //Layer
                    for (int j = 0; j < NeuronVal[i].Length; j++)
                    {
                        // Neuron
                        if (i < NeuronVal.Length - 1)
                        {
                            //Goes throught all the next errors to adjust the error coefficient
                            NeuronErrors[i][j] = 0;
                            for (int k = 0; k < NeuronVal[i + 1].Length; k++)
                                NeuronErrors[i][j] += NeuronErrors[i + 1][k] * Weigths[Nettype][i + 1][j, k];
                        }
                        for (int k = 0; k < Weigths[Nettype][i].GetLength(0); k++)
                        {
                            if (i == 0)
                                LastNeuronOut = MatrixOut[k];
                            else
                                LastNeuronOut = NeuronVal[i - 1][k];
                            //Change Weights
                            WeigthChanges[Nettype][i][k, j] -= Lernrate * LastNeuronOut * NeuronErrors[i][j] / TrainingInput.Length;
                        }
                        //Change the Bias
                        BiasChange[Nettype][i][j] -= Lernrate * NeuronErrors[i][j] / TrainingInput.Length;
                    }
                }
                //Calculate Net Matrix Error
                for (int i = 0; i < MatrixOut.Length; i++)
                {
                    for (int j = 0; j < NeuronErrors[0].Length; j++)
                    {
                        if (i < 512)
                            MatrixErrorA[i + 8] += NeuronErrors[0][j] * Weigths[Nettype][0][i, j];
                        else
                            MatrixErrorB[i - 504] += NeuronErrors[0][j] * Weigths[Nettype][0][i, j];
                    }
                }
                //Calculate Perspective error
                float PerspectiveHeadError = Error - OutError;
                MatrixErrorA[Nettype] += PerspectiveHeadError;
                MatrixErrorB[Nettype] -= PerspectiveHeadError;

                foreach (int Place in Input[0])
                    if (Place > -1)
                        for (int j = 0; j < MatrixChange.GetLength(1); j++)
                            MatrixChange[Place, j] -= Lernrate * MatrixErrorA[j] / (TrainingInput.Length * 2);

                foreach (int Place in Input[1])
                    if (Place > -1)
                        for (int j = 0; j < MatrixChange.GetLength(1); j++)
                            MatrixChange[Place, j] -= Lernrate * MatrixErrorB[j] / (TrainingInput.Length * 2);

            }
        }
    }
    public void BackPropagationHalfkp(TrainingPosition[] TrainingInput)
    {
        //init Connection deltas
        double[] MatrixErrorA = new double[256], MatrixErrorB = new double[256];
        double LastNeuronOut = 0;
        // init BiasChanges
        for (int j = 0; j < HalfkpBiases.Length; j++)
            HalfkpBiasChange[j] = new double[HalfkpFormat[j + 1]];

        // init WeightChanges
        for (int j = 0; j < 3; j++)
            HalfkpWeigthChanges[j] = new double[HalfkpFormat[j], HalfkpFormat[j + 1]];

        //Init start MatrixChanges
        HalfkpMatrixChange = new double[45056, 256];
        //Init Matrix Biases
        HalfkpMatrixBiasChange = new double[512];

        for (int gen = 0; gen < TrainingInput.Length; gen++)
        {
            float Value = TrainingInput[gen].Eval;
            float color = TrainingInput[gen].Color;
            List<int>[] Input = BoardToHalfKav2(TrainingInput[gen].Board, (byte)color);
            Input[0] = ConvertVectorToHalfkp(Input[0]);
            Input[1] = ConvertVectorToHalfkp(Input[1]);
            double NetOutput = UseHalfkpNet(TrainingInput[gen].Board, (byte)color);
            if (NetOutput != Value)
            {
                //ResetConnectionErrorDeltas
                MatrixErrorA = new double[256];
                MatrixErrorB = new double[256];

                double Error = (NetOutput - Value);
                HalfkpNeuronErrors[2][0] = Error;
                for (int i = HalfkpNeuronVal.Length - 1; i > -1; i--)
                {
                    //Layer
                    for (int j = 0; j < HalfkpNeuronVal[i].Length; j++)
                    {
                        // Neuron
                        if (i < HalfkpNeuronVal.Length - 1)
                        {
                            //Goes throught all the next errors to adjust the error coefficient
                            HalfkpNeuronErrors[i][j] = 0;
                            if (ReluDerivative(HalfkpNeuronVal[i][j]) == 1)
                                for (int k = 0; k < HalfkpNeuronVal[i + 1].Length; k++)
                                    HalfkpNeuronErrors[i][j] += HalfkpNeuronErrors[i + 1][k] * HalfkpWeigths[i + 1][j, k];
                        }
                        for (int k = 0; k < HalfkpWeigths[i].GetLength(0); k++)
                        {
                            if (i == 0)
                                LastNeuronOut = HalfkpMatrixOut[k];
                            else
                                LastNeuronOut = HalfkpNeuronVal[i - 1][k];
                            //Change Weights
                            HalfkpWeigthChanges[i][k, j] += LastNeuronOut * HalfkpNeuronErrors[i][j] / TrainingInput.Length;
                        }
                        //Change the Bias
                        HalfkpBiasChange[i][j] += HalfkpNeuronErrors[i][j] / TrainingInput.Length;
                    }
                }
                //Calculate Net Matrix Error
                for (int i = 0; i < HalfkpMatrixOut.Length; i++)
                {
                    if (ReluDerivative(HalfkpMatrixOut[i]) == 1)
                    {
                        for (int j = 0; j < HalfkpNeuronErrors[0].Length; j++)
                        {
                            if (i < 256)
                                MatrixErrorA[i] += HalfkpNeuronErrors[0][j] * HalfkpWeigths[0][i, j];
                            else
                                MatrixErrorB[i - 256] += HalfkpNeuronErrors[0][j] * HalfkpWeigths[0][i, j];
                        }
                    }
                }
                foreach (int Place in Input[0])
                    if (Place > -1)
                        for (int j = 0; j < HalfkpMatrixChange.GetLength(1); j++)
                            HalfkpMatrixChange[Place, j] += MatrixErrorA[j] / (TrainingInput.Length * 2);

                foreach (int Place in Input[1])
                    if (Place > -1)
                        for (int j = 0; j < HalfkpMatrixChange.GetLength(1); j++)
                            HalfkpMatrixChange[Place, j] += MatrixErrorB[j] / (TrainingInput.Length * 2);

                for (int i = 0; i < 512; i++)
                {
                    if (i < 256)
                        HalfkpMatrixBiasChange[i] += MatrixErrorA[i] / TrainingInput.Length;
                    else
                        HalfkpMatrixBiasChange[i] += MatrixErrorB[i - 256] / TrainingInput.Length;
                }
            }
        }
    }
    public void setNet(float[][][][,] WeightChanges, float[][][][] BiasChanges, float[][,] MatrixChanges, float Momentum)
    {
        float CurrentChange = 0;
        //Change Weigths
        for (int i = 0; i < Weigths.Length; i++)
        {

            for (int j = 0; j < Weigths[i].Length; j++)
            {
                //Layer
                for (int k = 0; k < Weigths[i][j].GetLength(1); k++)
                {
                    //Neuron
                    for (int m = 0; m < Weigths[i][j].GetLength(0); m++)
                    {
                        //Set New Weight
                        for (int l = 0; l < WeightChanges.Length; l++)
                            CurrentChange += WeightChanges[l][i][j][m, k] / WeightChanges.Length;

                        OldWeigths[i][j][m, k] = OldWeigths[i][j][m, k] * Momentum + (1 - Momentum) * CurrentChange;
                        Weigths[i][j][m, k] += OldWeigths[i][j][m, k];
                        CurrentChange = 0;

                    }
                    //Set New Bias
                    for (int l = 0; l < WeightChanges.Length; l++)
                        CurrentChange += BiasChanges[l][i][j][k] / WeightChanges.Length;

                    OldBiases[i][j][k] = Momentum * OldBiases[i][j][k] + (1 - Momentum) * CurrentChange;

                    Biases[i][j][k] += OldBiases[i][j][k];
                    CurrentChange = 0;
                }
            }
        }

        for (int i = 0; i < StartMatrix.GetLength(0); i++)
        {
            for (int j = 0; j < StartMatrix.GetLength(1); j++)
            {
                for (int l = 0; l < WeightChanges.Length; l++)
                    CurrentChange += MatrixChanges[l][i, j] / WeightChanges.Length;

                OldMatrix[i, j] = Momentum * OldMatrix[i, j] + (1 - Momentum) * CurrentChange;
                StartMatrix[i, j] += OldMatrix[i, j];
                CurrentChange = 0;
            }
        }
    }
    public void GradientDescent(double[][][,] WeightChanges, double[][][] BiasChanges, double[][,] MatrixChanges, double[][] MatrixBiasChanges, double Momentum, double learningRate, float weight_decay , float t_max)
    {
        double CurrentChange = 0, corrected_momentum = 0, update = 0, decay = 0;

        double norm = 0, largest_change = 0, smallest_change = 2;
        //caculate the current learningrate
        double currentLearningRate = learningRate;//(Math.Min(2 * (iterationcount % t_max) / t_max, 2 - 2 * (iterationcount % t_max) / t_max) * 2 + 1) * learningRate / 3;
        //lr range test
        //currentLearningRate = 0.001f * (float)Math.Pow(10000, (float)iterationcount / 1000);
        for (int i = 0; i < HalfkpWeigths.Length; i++)
        {
            //Layer
            for (int k = 0; k < HalfkpWeigths[i].GetLength(1); k++)
            {
                //Neuron
                for (int m = 0; m < HalfkpWeigths[i].GetLength(0); m++)
                {
                    //Set New Weight
                    for (int l = 0; l < WeightChanges.Length; l++)
                        CurrentChange += WeightChanges[l][i][m, k] / WeightChanges.Length;

                    //calculate the current momentum
                    HalfkpWeigth_momentum[i][m, k] = Momentum * HalfkpWeigth_momentum[i][m, k] + (1 - Momentum) * CurrentChange;
                    //correct the momentum bias
                    corrected_momentum = HalfkpWeigth_momentum[i][m, k] / (1 - (float)Math.Pow(Momentum, iterationcount + 1));
                    //calculate the update vector
                    update = corrected_momentum;
                    //calculate the norm
                    norm += update * update;

                    if (Math.Sqrt(update * update) > largest_change)
                        largest_change = Math.Sqrt(update * update);
                    else if (Math.Sqrt(update * update) < smallest_change)
                        smallest_change = Math.Sqrt(update * update);
                    //calculate the decay
                    decay = weight_decay * HalfkpWeigths[i][m, k];
                    //update the weights
                    HalfkpWeigths[i][m, k] -= currentLearningRate * (update - decay);
                    CurrentChange = 0;

                }
                //Set New Bias
                for (int l = 0; l < WeightChanges.Length; l++)
                    CurrentChange += BiasChanges[l][i][k] / WeightChanges.Length;

                //calculate the current momentum
                HalfkpBias_momentum[i][k] = Momentum * HalfkpBias_momentum[i][k] + (1 - Momentum) * CurrentChange;
                //correct the momentum bias
                corrected_momentum = HalfkpBias_momentum[i][k] / (1 - (float)Math.Pow(Momentum, iterationcount + 1));
                //calculate the update vector
                update = corrected_momentum;
                //calculate the norm
                norm += update * update;

                if (Math.Sqrt(update * update) > largest_change)
                    largest_change = Math.Sqrt(update * update);
                else if (Math.Sqrt(update * update) < smallest_change)
                    smallest_change = Math.Sqrt(update * update);
                //calculate the decay
                decay = decay = weight_decay * HalfkpBiases[i][k];
                //update the weights
                HalfkpBiases[i][k] -= currentLearningRate * (update - decay);
                CurrentChange = 0;
            }
        }

        for (int i = 0; i < HalfkpMatrix.GetLength(0); i++)
        {
            for (int j = 0; j < HalfkpMatrix.GetLength(1); j++)
            {
                for (int l = 0; l < WeightChanges.Length; l++)
                    CurrentChange += MatrixChanges[l][i, j] / WeightChanges.Length;

                //calculate the current momentum
                HalfkpMatrix_momentum[i, j] = Momentum * HalfkpMatrix_momentum[i, j] + (1 - Momentum) * CurrentChange;
                //correct the momentum bias
                corrected_momentum = HalfkpMatrix_momentum[i, j] / (1 - (float)Math.Pow(Momentum, iterationcount + 1));
                //calculate the update vector
                update = corrected_momentum;
                //calculate the norm
                norm += update * update;

                if (Math.Sqrt(update * update) > largest_change)
                    largest_change = Math.Sqrt(update * update);
                else if (Math.Sqrt(update * update) < smallest_change)
                    smallest_change = Math.Sqrt(update * update);
                //calculate the decay
                decay = weight_decay * HalfkpMatrix[i, j];
                //update the weights
                HalfkpMatrix[i, j] -= currentLearningRate * (update - decay);
                CurrentChange = 0;
            }
        }
        for (int j = 0; j < HalfkpMatrixBias.Length; j++)
        {
            for (int l = 0; l < MatrixBiasChanges.Length; l++)
                CurrentChange += MatrixBiasChanges[l][j] / WeightChanges.Length;

            //calculate the current momentum
            HalfkpMatrixBias_momentum[j] = Momentum * HalfkpMatrixBias_momentum[j] + (1 - Momentum) * CurrentChange;
            //correct the momentum bias
            corrected_momentum = HalfkpMatrixBias_momentum[j] / (1 - (float)Math.Pow(Momentum, iterationcount + 1));
            //calculate the update vector
            update = corrected_momentum;
            //calculate the norm
            norm += update * update;

            if (Math.Sqrt(update * update) > largest_change)
                largest_change = Math.Sqrt(update * update);
            else if (Math.Sqrt(update * update) < smallest_change)
                smallest_change = Math.Sqrt(update * update);
            //calculate the decay
            decay = weight_decay * HalfkpMatrixBias[j];
            //update the weights
            HalfkpMatrixBias[j] -= currentLearningRate * (update - decay);
            CurrentChange = 0;
        }
        //augmant the iteration count by 1
        iterationcount++;
        Console.WriteLine("largest change {0}", largest_change);
        Console.WriteLine("smallest change {0}", smallest_change);
        Console.WriteLine("the norm is {0}", Math.Sqrt(norm));
    }
}
class PSQTValue
{
    public string Name = "";
    public float Value = float.NaN;
}

