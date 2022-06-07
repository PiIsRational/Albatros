using System;
using System.IO;
using System.Collections.Generic;
class NNUE
{
    standart_chess chess_stuff = new standart_chess();
    Random random = new Random();
    //768 -> 128*2 -> 1
    //feature transformer
    public double[,] HalfkpMatrix = new double[768, 128], HalfkpMatrixChange = new double[768, 128], HalfkpMatrix_momentum = new double[768, 128], HalfkpMatrix_old_momentum = new double[768, 128];
    //featur transformer bias
    public double[] HalfkpMatrixBias = new double[128], HalfkpMatrixBiasChange = new double[128], HalfkpMatrixBias_momentum = new double[128], HalfkpMatrixBias_old_momentum = new double[128];
    //weights
    public double[][,] HalfkpWeigths = new double[1][,], HalfkpWeigthChanges = new double[1][,], HalfkpWeigth_momentum = new double[1][,], HalfkpWeigth_old_momentum = new double[1][,];
    //biases
    public double[][] HalfkpBiases = new double[1][], HalfkpBias_momentum = new double[1][], HalfkpBias_old_momentum = new double[1][], HalfkpBiasChange = new double[1][];

    public double[][] HalfkpNeuronInput = new double[1][], HalfkpNeuronVal = new double[1][], HalfkpNeuronErrors = new double[1][];
    public double[] HalfkpMatrixOut = new double[256];

    public double HalfkpOutNet = 0, iterationcount = 0;
    int[] HalfkpFormat = new int[] { 256, 1 };
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
            HalfkpBias_old_momentum[i] = new double[HalfkpFormat[i + 1]];
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
            HalfkpWeigth_old_momentum[i] = new double[HalfkpFormat[i], HalfkpFormat[i + 1]];
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
    public double UseNet(byte[,] InputBoard, byte color)
    {
        List<int>[] Input = BoardToHalfP(InputBoard);

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

        // Return the Output as a Relu
        return HalfkpOutNet;
    }
    public List<int>[] BoardToHalfP(byte[,] InputBoard)
    {
        List<int>[] Features = new List<int>[2];

        for (int color = 0; color <= 1; color++)
        {
            Features[color] = new List<int>();
            for (int i = 1; i < 9; i++)
            {
                for (int j = 1; j < 9; j++)
                {
                    if (PieceType[color][InputBoard[i, j]] > -1)
                    {
                        Features[color].Add(PieceType[color][InputBoard[i, j]] + (7 * (1 - color) + (2 * color - 1) * (j - 1)) * 8 + i - 1);
                    }
                }
            }
        }

        return Features;
    }

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
                        PieceType[i][j] = ChangeType(6, i);
                        break;
                    case 0b00000010:
                        PieceType[i][j] = ChangeType(6, i);
                        break;
                    case 0b00000011:
                        PieceType[i][j] = ChangeType(6, i);
                        break;
                    case 0b00000100:
                        PieceType[i][j] = ChangeType(7, i);
                        break;
                    case 0b00000101:
                        PieceType[i][j] = ChangeType(8, i);
                        break;
                    case 0b00001000:
                        PieceType[i][j] = ChangeType(9, i);
                        break;
                    case 0b00001001:
                        PieceType[i][j] = ChangeType(10, i);
                        break;
                    case 0b00001010:
                        PieceType[i][j] = ChangeType(10, i);
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
                        PieceType[i][j] = ChangeType(11, i);
                        break;
                    case 0b00000111:
                        PieceType[i][j] = ChangeType(11, i);
                        break;
                    case 0b00010111:
                        PieceType[i][j] = ChangeType(5, i);
                        break;
                    case 0b00010110:
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
            if (piecetype - 4 > 0)
                return (piecetype -= 5) * 64;
            else
                return (piecetype += 5) * 64;
        }
        else
            return piecetype * 64;
    }
    public double ReluDerivative(double Input)
    {
        if (Input <= 0 || Input >= 1)
            return 0;
        else
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
    }
    public void OpenNet(string FileName)
    {
        int counter = 0;
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
                counter++;
            }
        }

        //StartMatrix
        for (int i = 0; i < HalfkpMatrix.GetLength(0); i++)
        {
            for (int j = 0; j < HalfkpMatrix.GetLength(1); j++)
            {
                HalfkpMatrix[i, j] = BitConverter.Int32BitsToSingle(Convert.ToInt32(Values[counter]));
                counter++;
            }
        }

        //StartMatrixBias
        for (int j = 0; j < HalfkpMatrixBias.Length; j++) 
        {
            HalfkpMatrixBias[j] = BitConverter.Int32BitsToSingle(Convert.ToInt32(Values[counter]));
            counter++;
        }
    }
    public double LargeSigmoid(float Input , float Size)
    {
        return (Input/ Size) / (float)Math.Sqrt((Input / Size) * (Input / Size) + 1);
    }
    public double[] CostOfNet(TrainingPosition[] Input)
    {
        Classic_Eval eval = new Classic_Eval();
        double Cost = 0, StaticEvalCost = 0, smallest_cost = 1, largest_cost = 0, smallest_cost_e = 1, largest_cost_e = 0;
        foreach (TrainingPosition TrainingExample in Input)
        {
            double color = TrainingExample.Color;
            double Output = LargeClippedRelu(UseNet(TrainingExample.Board, (byte)color));
            double Value = TrainingExample.Eval;
            double CurrentCost = (Value - Output) * (Value - Output);

            if (CurrentCost > largest_cost)
                largest_cost = CurrentCost;
            else if (CurrentCost < smallest_cost)
                smallest_cost = CurrentCost;
            Cost += CurrentCost;

            Cost += CurrentCost;
            Output = chess_stuff.convert_millipawn_to_wdl(eval.pesto_eval(TrainingExample.Board, (byte)color));
            CurrentCost = (Value - Output) * (Value - Output);

            if (CurrentCost > largest_cost_e)
                largest_cost_e = CurrentCost;
            else if (CurrentCost < smallest_cost_e)
                smallest_cost_e = CurrentCost;

            StaticEvalCost += CurrentCost;
        }
        Console.WriteLine("smallest cost of the neural net {0}", smallest_cost);
        Console.WriteLine("largest cost of the neural net {0}", largest_cost);
        Console.WriteLine("smallest cost of the static evaluation {0}", smallest_cost_e);
        Console.WriteLine("largest cost of the static evaluation {0}", largest_cost_e);
        return new double[2] { Cost / Input.Length, StaticEvalCost / Input.Length };
    }
    public void BackPropagation(TrainingPosition[] TrainingInput)
    {
        Random random = new Random();
        //init Connection deltas
        double[] MatrixErrorA = new double[128], MatrixErrorB = new double[128];
        double LastNeuronOut = 0;
        // init BiasChanges
        for (int j = 0; j < HalfkpBiases.Length; j++)
            HalfkpBiasChange[j] = new double[HalfkpFormat[j + 1]];

        // init WeightChanges
        for (int j = 0; j < HalfkpWeigths.Length; j++)
            HalfkpWeigthChanges[j] = new double[HalfkpFormat[j], HalfkpFormat[j + 1]];

        //Init start MatrixChanges
        HalfkpMatrixChange = new double[768, 128];
        //Init Matrix Biases
        HalfkpMatrixBiasChange = new double[128];

        for (int gen = 0; gen < TrainingInput.Length; gen++)
        {
            float Value = TrainingInput[gen].Eval;
            float color = TrainingInput[gen].Color;
            List<int>[] Input = BoardToHalfP(TrainingInput[gen].Board);
            double NetOutput = LargeClippedRelu((float)UseNet(TrainingInput[gen].Board, (byte)color));
            if (NetOutput != Value)
            {
                //ResetConnectionErrorDeltas
                MatrixErrorA = new double[128];
                MatrixErrorB = new double[128];

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
                            if (i < 128)
                                MatrixErrorA[i] += HalfkpNeuronErrors[0][j] * HalfkpWeigths[0][i, j];
                            else
                                MatrixErrorB[i - 128] += HalfkpNeuronErrors[0][j] * HalfkpWeigths[0][i, j];
                        }
                    }
                }
                foreach (int Place in Input[(int)color])
                    if (Place > -1)
                        for (int j = 0; j < HalfkpMatrixChange.GetLength(1); j++)
                            HalfkpMatrixChange[Place, j] += MatrixErrorA[j] / (TrainingInput.Length * 2);

                foreach (int Place in Input[1 - (int)color]) 
                    if (Place > -1)
                        for (int j = 0; j < HalfkpMatrixChange.GetLength(1); j++)
                            HalfkpMatrixChange[Place, j] += MatrixErrorB[j] / (TrainingInput.Length * 2);

                for (int i = 0; i < 128; i++) 
                {
                    HalfkpMatrixBiasChange[i] += MatrixErrorA[i] / (2 * TrainingInput.Length);
                    HalfkpMatrixBiasChange[i] += MatrixErrorB[i] / (2 * TrainingInput.Length);
                }
            }
        }
    }
    public void GradientDescent(double[][][,] WeightChanges, double[][][] BiasChanges, double[][,] MatrixChanges, double[][] MatrixBiasChanges, double Momentum, double learningRate, float weight_decay , float t_max)
    {
        double CurrentChange = 0, corrected_momentum = 0, update = 0, decay = 0;

        double norm = 0, largest_change = 0, smallest_change = 2;
        //caculate the current learningrate
        double currentLearningRate = (Math.Min(2 * (iterationcount % t_max) / t_max, 2 - 2 * (iterationcount % t_max) / t_max) * 2 + 1) * learningRate / 3;
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
        /*
        Console.WriteLine("largest change {0}", largest_change);
        Console.WriteLine("smallest change {0}", smallest_change);
        Console.WriteLine("the norm is {0}", Math.Sqrt(norm));*/
    }
    public double LargeClippedRelu(double Input)
    {
        return Math.Max(Math.Min(Input, 0.999f), -0.999f);
    }
}

