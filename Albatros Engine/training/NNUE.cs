using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
class NNUE
{
    standart_chess chess_stuff = new standart_chess();
    standart stuff = new standart();
    Random random = new Random();
    //768 -> 128*2 -> 1
    //feature transformer
    public double[,] HalfkpMatrix = new double[768, 16], HalfkpMatrixChange = new double[768, 16], HalfkpMatrix_momentum = new double[768, 16], HalfkpMatrix_velocity = new double[768, 16];
    //featur transformer bias
    public double[] HalfkpMatrixBias = new double[16], HalfkpMatrixBiasChange = new double[16], HalfkpMatrixBias_momentum = new double[16], HalfkpMatrixBias_velocity = new double[16];
    //weights
    public double[][,] HalfkpWeigths = new double[1][,], HalfkpWeigthChanges = new double[1][,], HalfkpWeigth_momentum = new double[1][,], HalfkpWeigth_velocity = new double[1][,];
    //biases
    public double[][] HalfkpBiases = new double[1][], HalfkpBias_momentum = new double[1][], HalfkpBias_velocity = new double[1][], HalfkpBiasChange = new double[1][];

    public double[][] HalfkpNeuronInput = new double[1][], HalfkpNeuronVal = new double[1][], HalfkpNeuronErrors = new double[1][];
    public double[] HalfkpMatrixOut = new double[32];

    public double HalfkpOutNet = 0, iterationcount = 0;
    int[] HalfkpFormat = new int[] { 32, 1 };
    int weight_scaling = 4096;
    int activation_scaling = 32767;
    //returns the piecetype of the halfp encoding
    //piecetype[0] is for black and piecetype[1] is for white
    int[][] PieceType = new int[2][];
    public NNUE()
    {
        //init HalfKp Biases
        for (int i = 0; i < HalfkpBiases.Length; i++)
        {
            HalfkpBiases[i] = new double[HalfkpFormat[i + 1]];
            HalfkpBias_momentum[i] = new double[HalfkpFormat[i + 1]];
            HalfkpBias_velocity[i] = new double[HalfkpFormat[i + 1]];
            HalfkpNeuronVal[i] = new double[HalfkpFormat[i + 1]];
            HalfkpNeuronInput[i] = new double[HalfkpFormat[i + 1]];
            HalfkpNeuronErrors[i] = new double[HalfkpFormat[i + 1]];
            for (int j = 0; j < HalfkpBiases[i].Length; j++)
            {
                HalfkpBiases[i][j] = Convert.ToSingle(((float)random.NextDouble() - 0.5) * 1 / 2);
            }
        }
        //init HalfKp Weights
        for (int i = 0; i < HalfkpWeigths.Length; i++)
        {
            HalfkpWeigth_momentum[i] = new double[HalfkpFormat[i], HalfkpFormat[i + 1]];
            HalfkpWeigth_velocity[i] = new double[HalfkpFormat[i], HalfkpFormat[i + 1]];
            HalfkpWeigthChanges[i] = new double[HalfkpFormat[i], HalfkpFormat[i + 1]];
            HalfkpWeigths[i] = new double[HalfkpFormat[i], HalfkpFormat[i + 1]];
            for (int j = 0; j < HalfkpWeigths[i].GetLength(0); j++)
            {
                for (int k = 0; k < HalfkpWeigths[i].GetLength(1); k++)
                {
                    HalfkpWeigths[i][j, k] = Convert.ToSingle(((float)random.NextDouble() - 0.5) * 1 / 2);
                }
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
        for (int i = 0; i < HalfkpMatrixBias.Length; i++)
            HalfkpMatrixBias[i] = Convert.ToSingle(((float)random.NextDouble() - 0.5) * 1 / 2);
        //initialize the position to feature vector converter
        initPtype();
    }
    public double[] MatrixMultiply(List<int> InputVector)
    {
        double[] Output = new double[HalfkpMatrixBias.Length];
        Array.Copy(HalfkpMatrixBias, Output, Output.Length);

        foreach (int Place in InputVector)
            if (Place > -1)
                for (int j = 0; j < Output.Length; j++)
                    Output[j] += HalfkpMatrix[Place, j];

        return Output;
    }
    public double Eval(double[] Input)
    {
        for (int i = 0; i < HalfkpWeigths.Length; i++)
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
        return HalfkpNeuronInput[0][0];
    }
    public double UseNet(byte[] board, byte color)
    {
        List<int>[] Input = BoardToHalfP(board);

        double[] EvalVector = new double[2 * HalfkpMatrix.GetLength(1)], ConverterOur = new double[HalfkpMatrix.GetLength(1)], ConverterTheir = new double[HalfkpMatrix.GetLength(1)];

        ConverterOur = MatrixMultiply(Input[color]);
        ConverterTheir = MatrixMultiply(Input[1 - color]);

        for (int i = 0; i < HalfkpMatrix.GetLength(1); i++)
        {
            EvalVector[i] = ConverterOur[i];
            EvalVector[i + HalfkpMatrix.GetLength(1)] = ConverterTheir[i];
        }

        for (int i = 0; i < EvalVector.Length; i++)
            EvalVector[i] = ClippedReLU(EvalVector[i]);

        Array.Copy(EvalVector, HalfkpMatrixOut, EvalVector.Length);
        HalfkpOutNet = Eval(EvalVector);

        // Return the Output
        return HalfkpOutNet;
    }
    public List<int>[] BoardToHalfP(byte[] board)
    {
        List<int>[] Features = new List<int>[2];
        for (int color = 0; color <= 1; color++)
        {
            Features[color] = new List<int>();

            for (int square = 0; square < 64; square++)
            {
                if (PieceType[color][board[square]] > -1)
                    Features[color].Add(PieceType[color][board[square]] + square ^ (color == 1 ? 0 : 56));
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
                    case standart_chess.bishop | 0b0000:
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
                        PieceType[i][j] = ChangeType(6, i);
                        break;
                    case standart_chess.knight | 0b1000:
                        PieceType[i][j] = ChangeType(7, i);
                        break;
                    case standart_chess.bishop | 0b1000:
                        PieceType[i][j] = ChangeType(8, i);
                        break;
                    case standart_chess.rook | 0b1000:
                        PieceType[i][j] = ChangeType(9, i);
                        break;
                    case standart_chess.queen | 0b1000:
                        PieceType[i][j] = ChangeType(10, i);
                        break;
                    case standart_chess.king | 0b1000:
                        PieceType[i][j] = ChangeType(11, i);
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
    public double ReluDerivative(double Input)
    {
        if (Input <= 0 || Input >= 1)
            return 0;

        return 1;
    }
    public double ClippedReLU(double Input)
    {
        return Math.Max(Math.Min(Input, 1), 0); 
    }
    public void SaveNet(string FileName, bool UseBackup)
    {
        StreamWriter sw = new StreamWriter(FileName, false, System.Text.Encoding.UTF8);
        StreamWriter swBackup = new StreamWriter("Nothing");
        if (UseBackup)
            swBackup = new StreamWriter("Backup.nnue", false, System.Text.Encoding.UTF8);
        bool start = true;
        string FileContent = "";
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
                        FileContent = "";
                        start = false;
                    }
                }
                for (int l = 0; l < HalfkpWeigths[j].GetLength(1); l++)
                {
                    byte[] arr = BitConverter.GetBytes(Convert.ToInt16(MaxMinShort((float)HalfkpWeigths[j][k, l] * weight_scaling)));
                    for (byte i = 0; i < arr.Length; i++)
                        FileContent += (char)arr[i];
                }
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
            {
                byte[] arr = BitConverter.GetBytes(Convert.ToInt32(MaxMinInt((float)HalfkpBiases[j][k] * weight_scaling * activation_scaling)));
                for (byte i = 0; i < arr.Length; i++)
                    FileContent += (char)arr[i];
            }
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
            {
                byte[] arr = BitConverter.GetBytes(Convert.ToInt32(MaxMinInt((float)HalfkpMatrix[i, j] * weight_scaling * activation_scaling)));
                for (byte k = 0; k < arr.Length; k++)
                    FileContent += (char)arr[k];
            }
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
        {
            byte[] arr = BitConverter.GetBytes(Convert.ToInt32(MaxMinInt((float)HalfkpMatrixBias[j] * weight_scaling * activation_scaling)));
            for (byte i = 0; i < arr.Length; i++)
                FileContent += (char)arr[i];
        }
        sw.Write(FileContent);
        FileContent = "";
        sw.Write(FileContent);

        sw.Flush();
        sw.Close();
        swBackup.Flush();
        swBackup.Close();
    }
    public void visualize_piece_values(bool show_matrix)
    {
        string[] piece_names = new string[12] { "white pawn", "white knight", "white bishop", "white queen", "white rook", "white king", "black pawn", "black knight", "black bishop", "black queen", "black rook", "black king" };
        double pawn_value = 0;
        //loop through each piece
        for (int piece = 0; piece < 12; piece++) 
        {
            //calculate the mean value of the piece
            double value = 0;
            for (int i = 0; i < 64; i++)
            {
                value += HalfkpMatrix[piece * 64 + i, 0];
               // value += HalfkpMatrix[(piece + 6) * 64 + i, 0];
            }

            value /= 64;

            if (piece == 0 || piece == 6)
                pawn_value = Math.Abs(value);

            Console.WriteLine("the value of the {0} is {1}", piece_names[piece], Math.Round(value / pawn_value, 2));

            if (show_matrix)
            {
                Console.WriteLine("the table of the {0} is:", piece_names[piece]);
                for (int i = 0; i < 8; i++)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        Console.Write("\t" + Math.Round((HalfkpMatrix[piece * 64 + i * 8 + j, 0] /*HalfkpMatrix[(piece + 6) * 64 + i * 8 + j, 0] - 2 **/ - value) / (/*2 **/ pawn_value), 2));
                    }
                    Console.WriteLine();
                }
                Console.WriteLine("\n");
            }
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
    public void OpenNet(string FileName)
    {
        StreamReader sr = new StreamReader(FileName);
     
        //Weights
        for (int j = 0; j < HalfkpWeigths.Length; j++)
        {
            for (int k = 0; k < HalfkpWeigths[j].GetLength(0); k++)
            {
                for (int l = 0; l < HalfkpWeigths[j].GetLength(1); l++)
                {
                    byte[] arr = new byte[2];
                    for (int i = 0; i < arr.Length; i++)
                        arr[i] = (byte)sr.Read();

                    HalfkpWeigths[j][k, l] = Convert.ToSingle(BitConverter.ToInt16(arr)) / weight_scaling;
                }
            }
        }

        //Biases
        for (int j = 0; j < HalfkpBiases.Length; j++)
        {
            for (int k = 0; k < HalfkpBiases[j].Length; k++)
            {
                byte[] arr = new byte[4];
                for (int i = 0; i < arr.Length; i++)
                    arr[i] = (byte)sr.Read();

                HalfkpBiases[j][k] = Convert.ToSingle(BitConverter.ToInt32(arr)) / (weight_scaling * activation_scaling);
            }
        }

        //StartMatrix
        for (int i = 0; i < HalfkpMatrix.GetLength(0); i++)
        {
            for (int j = 0; j < HalfkpMatrix.GetLength(1); j++)
            {
                byte[] arr = new byte[4];
                for (int k = 0; k < arr.Length; k++)
                    arr[k] = (byte)sr.Read();

                HalfkpMatrix[i, j] = Convert.ToSingle(BitConverter.ToInt32(arr)) / (weight_scaling * activation_scaling);
            }
        }

        //StartMatrixBias
        for (int j = 0; j < HalfkpMatrixBias.Length; j++) 
        {
            byte[] arr = new byte[4];
            for (int k = 0; k < arr.Length; k++)
                arr[k] = (byte)sr.Read();

            HalfkpMatrixBias[j] = Convert.ToSingle(BitConverter.ToInt32(arr)) / (weight_scaling * activation_scaling);
        }

        sr.Close();
    }
    public double[] CostOfNet(TrainingPosition[] Input, double lambda)
    {
        Classic_Eval eval = new Classic_Eval();
        double Cost = 0, StaticEvalCost = 0, smallest_cost = 1, largest_cost = 0, smallest_cost_e = 1, largest_cost_e = 0;
        float w = 0, d = 0, l = 0;
        int mate = 0;
        double l_two_norm = 0;
        foreach (TrainingPosition TrainingExample in Input)
        {
            if (Math.Abs(TrainingExample.Eval) != 1)
            {
                double color = TrainingExample.Color;
                double Output = UseNet(TrainingExample.Board, (byte)color);
                double Value = (float)(lambda * TrainingExample.Eval + (1 - lambda) * (Convert.ToDouble(TrainingExample.Result) - 1));
                double CurrentCost = (Value - Output) * (Value - Output);
                if (Convert.ToDouble(TrainingExample.Result) - 1 == 1)
                    w++;
                else if (Convert.ToDouble(TrainingExample.Result) - 1 == 0)
                    d++;
                else
                    l++;

                //l_two_norm += CurrentCost * CurrentCost;
                Cost += CurrentCost;
                Output = chess_stuff.convert_millipawn_to_wdl(eval.pesto_eval(TrainingExample.Board, (byte)color));
                CurrentCost = (Value - Output) * (Value - Output);


                StaticEvalCost += CurrentCost;
            }
            else
                mate++;
        }
        /*Console.WriteLine("smallest cost of the neural net {0}", smallest_cost);
        Console.WriteLine("largest cost of the neural net {0}", largest_cost);
        Console.WriteLine("smallest cost of the static evaluation {0}", smallest_cost_e);
        Console.WriteLine("largest cost of the static evaluation {0}", largest_cost_e);
        Console.WriteLine("w {0} d {1} l {2}", w, d, l);
        Console.WriteLine("the standard deviation of the nn cost is {0}", Math.Sqrt(l_two_norm / Input.Length - (Cost / Input.Length) * (Cost / Input.Length)));*/
        //Console.WriteLine(mate);
        return new double[2] { Cost / (Input.Length - mate), StaticEvalCost / (Input.Length - mate) };
    }
    public void BackPropagation(TrainingPosition[] TrainingInput, float lambda)
    {
        Random random = new Random();
        //init Connection deltas
        double[] MatrixErrorA = new double[16], MatrixErrorB = new double[16];
        double LastNeuronOut = 0;
        // init BiasChanges
        for (int j = 0; j < HalfkpBiases.Length; j++)
            HalfkpBiasChange[j] = new double[HalfkpFormat[j + 1]];

        // init WeightChanges
        for (int j = 0; j < HalfkpWeigths.Length; j++)
            HalfkpWeigthChanges[j] = new double[HalfkpFormat[j], HalfkpFormat[j + 1]];

        //Init start MatrixChanges
        HalfkpMatrixChange = new double[768, 16];
        //Init Matrix Biases
        HalfkpMatrixBiasChange = new double[16];

        for (int gen = 0; gen < TrainingInput.Length; gen++)
        {
            if (Math.Abs(TrainingInput[gen].Eval) == 1)
                continue;

            float Value = lambda * TrainingInput[gen].Eval + (1 - lambda) * (Convert.ToSingle(TrainingInput[gen].Result) - 1);
            float color = TrainingInput[gen].Color;
            List<int>[] Input = BoardToHalfP(TrainingInput[gen].Board);
            double NetOutput = (float)UseNet(TrainingInput[gen].Board, (byte)color);

            //ResetConnectionErrorDeltas
            MatrixErrorA = new double[16];
            MatrixErrorB = new double[16];

            double Error = 2 * (NetOutput - Value);
            HalfkpNeuronErrors[0][0] = Error;
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
                        for (int k = 0; k < HalfkpNeuronVal[i + 1].Length; k++)
                            HalfkpNeuronErrors[i][j] += ReluDerivative(HalfkpNeuronVal[i][j]) * HalfkpNeuronErrors[i + 1][k] * HalfkpWeigths[i + 1][j, k];
                    }
                    for (int k = 0; k < HalfkpWeigths[i].GetLength(0); k++)
                    {
                        if (i == 0)
                            LastNeuronOut = HalfkpMatrixOut[k];
                        else
                            LastNeuronOut = HalfkpNeuronVal[i - 1][k];
                        //Change Weights
                        HalfkpWeigthChanges[i][k, j] += LastNeuronOut * HalfkpNeuronErrors[i][j];
                    }
                    //Change the Bias
                    HalfkpBiasChange[i][j] += HalfkpNeuronErrors[i][j];
                }
            }
            //Calculate Net Matrix Error
            for (int i = 0; i < HalfkpMatrixOut.Length; i++)
            {
                for (int j = 0; j < HalfkpNeuronErrors[0].Length; j++)
                {
                    if (i < 16)
                        MatrixErrorA[i] += ReluDerivative(HalfkpMatrixOut[i]) * HalfkpNeuronErrors[0][j] * HalfkpWeigths[0][i, j];
                    else
                        MatrixErrorB[i - 16] += ReluDerivative(HalfkpMatrixOut[i]) * HalfkpNeuronErrors[0][j] * HalfkpWeigths[0][i, j];
                }

            }

            foreach (int Place in Input[(int)color])
                if (Place > -1)
                    for (int j = 0; j < HalfkpMatrixChange.GetLength(1); j++)
                        HalfkpMatrixChange[Place, j] += MatrixErrorA[j] / 2;

            foreach (int Place in Input[(int)color ^ 1])
                if (Place > -1)
                    for (int j = 0; j < HalfkpMatrixChange.GetLength(1); j++)
                        HalfkpMatrixChange[Place, j] += MatrixErrorB[j] / 2;

            for (int i = 0; i < 8; i++)
            {
                HalfkpMatrixBiasChange[i] += MatrixErrorA[i] / 2;
                HalfkpMatrixBiasChange[i] += MatrixErrorB[i] / 2;
            }
        }
    }
    public void GradientDescent(double[][][,] WeightChanges, double[][][] BiasChanges, double[][,] MatrixChanges, double[][] MatrixBiasChanges, double Momentum, double velocity, double learningRate, float weight_decay, float t_max, int sample_count)
    {    
        double CurrentChange = 0, corrected_momentum = 0, update = 0, decay = 0, corrected_second_momentum = 0;
        double epsilon = 1 / Math.Pow(10, 8); 
        int divisor = sample_count;
        //double norm = 0, largest_change = 0, smallest_change = 2;
        //caculate the current learningrate
        double currentLearningRate = learningRate;// (Math.Min(2 * (iterationcount % t_max) / t_max, 2 - 2 * (iterationcount % t_max) / t_max) * 2 + 1) * learningRate / 3;
        //lr range test
        //currentLearningRate = 0.001f * (float)Math.Pow(1000, (float)iterationcount / 2000);
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
                        CurrentChange += WeightChanges[l][i][m, k] / divisor;

                    //calculate the current momentum
                    HalfkpWeigth_momentum[i][m, k] = Momentum * HalfkpWeigth_momentum[i][m, k] + (1 - Momentum) * CurrentChange;
                    //correct the momentum bias
                    corrected_momentum = HalfkpWeigth_momentum[i][m, k] / (1 - (float)Math.Pow(Momentum, iterationcount + 1));
                    //calculate 
                    HalfkpWeigth_velocity[i][m, k] = velocity * HalfkpWeigth_velocity[i][m, k] + (1 - velocity) * CurrentChange * CurrentChange;
                    //correct the momentum
                    corrected_second_momentum = HalfkpWeigth_velocity[i][m, k] / (1 - (float)Math.Pow(velocity, iterationcount + 1));
                    //calculate the update vector
                    update = corrected_momentum / (Math.Sqrt(corrected_second_momentum) + epsilon);
                    //calculate the norm
                    /*norm += update * update;

                    if (Math.Abs(update) > largest_change)
                        largest_change = Math.Abs(update);
                    else if (Math.Abs(update) < smallest_change)
                        smallest_change = Math.Abs(update);*/

                    //calculate the decay
                    decay = weight_decay * HalfkpWeigths[i][m, k];
                    //update the weights
                    HalfkpWeigths[i][m, k] -= currentLearningRate * (update + decay);
                    HalfkpWeigths[i][m, k] = clip_weights(HalfkpWeigths[i][m, k]);
                    CurrentChange = 0;

                }
                //Set New Bias
                for (int l = 0; l < WeightChanges.Length; l++)
                    CurrentChange += BiasChanges[l][i][k] / divisor;

                //calculate the current momentum
                HalfkpBias_momentum[i][k] = Momentum * HalfkpBias_momentum[i][k] + (1 - Momentum) * CurrentChange;
                //correct the momentum bias
                corrected_momentum = HalfkpBias_momentum[i][k] / (1 - (float)Math.Pow(Momentum, iterationcount + 1));
                //calculate 
                HalfkpBias_velocity[i][k] = velocity * HalfkpBias_velocity[i][k] + (1 - velocity) * CurrentChange * CurrentChange;
                //correct the momentum
                corrected_second_momentum = HalfkpBias_velocity[i][k] / (1 - (float)Math.Pow(velocity, iterationcount + 1));
                //calculate the update vector
                update = corrected_momentum / (Math.Sqrt(corrected_second_momentum) + epsilon);
                //calculate the norm
                /*norm += update * update;

                if (Math.Abs(update) > largest_change)
                    largest_change = Math.Abs(update);
                else if (Math.Abs(update) < smallest_change)
                    smallest_change = Math.Abs(update);*/

                //calculate the decay
                decay = weight_decay * HalfkpBiases[i][k];
                //update the weights
                HalfkpBiases[i][k] -= currentLearningRate * (update + decay);
                HalfkpBiases[i][k] = clip_the_rest(HalfkpBiases[i][k]);
                CurrentChange = 0;
            }
        }
        
        for (int i = 0; i < HalfkpMatrix.GetLength(0); i++)
        {
            for (int j = 0; j < HalfkpMatrix.GetLength(1); j++)
            {
                for (int l = 0; l < WeightChanges.Length; l++)
                    CurrentChange += MatrixChanges[l][i, j] / divisor;

                //calculate the current momentum
                HalfkpMatrix_momentum[i, j] = Momentum * HalfkpMatrix_momentum[i, j] + (1 - Momentum) * CurrentChange;
                //correct the momentum bias
                corrected_momentum = HalfkpMatrix_momentum[i, j] / (1 - (float)Math.Pow(Momentum, iterationcount + 1));
                //calculate 
                HalfkpMatrix_velocity[i, j] = velocity * HalfkpMatrix_velocity[i, j] + (1 - velocity) * CurrentChange * CurrentChange;
                //correct the momentum
                corrected_second_momentum = HalfkpMatrix_velocity[i, j] / (1 - (float)Math.Pow(velocity, iterationcount + 1));
                //calculate the update vector
                update = corrected_momentum / (Math.Sqrt(corrected_second_momentum) + epsilon);
                //calculate the norm
                /*norm += update * update;

                if (Math.Abs(update) > largest_change)
                    largest_change = Math.Abs(update);
                else if (Math.Abs(update) < smallest_change)
                    smallest_change = Math.Abs(update);*/

                //calculate the decay
                decay = weight_decay * HalfkpMatrix[i, j];
                //update the weights
                HalfkpMatrix[i, j] -= currentLearningRate * (update + decay);
                HalfkpMatrix[i, j] = clip_the_rest(HalfkpMatrix[i, j]);
                CurrentChange = 0;
            }
        }
        for (int j = 0; j < HalfkpMatrixBias.Length; j++)
        {
            for (int l = 0; l < MatrixBiasChanges.Length; l++)
                CurrentChange += MatrixBiasChanges[l][j] / divisor;

            //calculate the current momentum
            HalfkpMatrixBias_momentum[j] = Momentum * HalfkpMatrixBias_momentum[j] + (1 - Momentum) * CurrentChange;
            //correct the momentum bias
            corrected_momentum = HalfkpMatrixBias_momentum[j] / (1 - (float)Math.Pow(Momentum, iterationcount + 1));
            //calculate 
            HalfkpMatrixBias_velocity[j] = velocity * HalfkpMatrixBias_velocity[j] + (1 - velocity) * CurrentChange * CurrentChange;
            //correct the momentum
            corrected_second_momentum = HalfkpMatrixBias_velocity[j] / (1 - (float)Math.Pow(velocity, iterationcount + 1));
            //calculate the update vector
            update = corrected_momentum / (Math.Sqrt(corrected_second_momentum) + epsilon);
            //calculate the norm
            /*norm += update * update;

            if (Math.Abs(update) > largest_change)
                largest_change = Math.Abs(update);
            else if (Math.Abs(update) < smallest_change)
                smallest_change = Math.Abs(update);*/

            //calculate the decay
            decay = weight_decay * HalfkpMatrixBias[j];
            //update the weights
            HalfkpMatrixBias[j] -= currentLearningRate * (update + decay);
            HalfkpMatrixBias[j] = clip_the_rest(HalfkpMatrixBias[j]);
            CurrentChange = 0;
        }
        //augmant the iteration count by 1
        iterationcount++;
        /*
        Console.WriteLine("largest change {0}", largest_change);
        Console.WriteLine("smallest change {0}", smallest_change);
        Console.WriteLine("the norm is {0}", Math.Sqrt(norm));*/
    }
    public double LargeClippedRelu(double Input)
    {
        return Math.Max(Math.Min(Input, 0.999f), -0.999f);
    }
    public double clip_weights(double input)
    {
        return Math.Max(Math.Min(input, 8), -8);
    }
    public double clip_the_rest(double input)
    {
        return Math.Max(Math.Min(input, 16), -16);
    }
}

