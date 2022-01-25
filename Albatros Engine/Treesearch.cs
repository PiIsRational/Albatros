using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.IO;

class Treesearch
{
    /*
     * Pieces:
     * Encoded in 5-bits(variable Byte)
     * <================>
     * NoPiece:             0b00000000
     * 
     * Black:
     * 
     * PawnStart:           0b00000001
     * PawnEnPassent:       0b00000010
     * PawnNormal:          0b00000011
     * Knight:              0b00000100
     * Bishop:              0b00000101
     * KingCanCastle:       0b00000110
     * King:                0b00000111
     * Queen:               0b00001000
     * RookCanCastle:       0b00001001
     * Rook:                0b00001010
     * 
     * White:
     * 
     * PawnStart:           0b00010001
     * PawnEnPassent:       0b00010010
     * PawnNormal           0b00010011
     * Knight:              0b00010100
     * Bishop:              0b00010101
     * KingCanCastle:       0b00010110
     * King:                0b00010111
     * Queen:               0b00011000
     * RookCanCastle:       0b00011001
     * Rook:                0b00011010
     * 
     * Board:
     * Byte[,] = new Byte[9,9]
     * Mooves:
     * PositionY = 8 // Queening
     * PositionY = 1 // Queening
     * 
     * If Moove Format is int[3] then special Moove involving two pieces
     * Unmove has the format X_a , Y_a , Piece_a , X_b , Y_b , Piece_b
     */
    public Classic_Eval eval = new Classic_Eval();
    public Node CurrentTree;
    public NNUE ValueNet = new NNUE();
    public BranchThread[] ThreadTreesearches;
    public Semaphore StopSemaphore = new Semaphore(1, 1);
    public MoveGen MoveGenerator = new MoveGen();
    BranchThread MainBranch;
    public Random random;
    bool stop = false;
    public bool wasStopped = false;
    public bool NetType = true;
    int Nodecount = 0;


    public Treesearch(int seed, bool LoadNet ,int ThreadCount)
    {
        random = new Random(seed);
        if (LoadNet)
        {
            //if the file exists
            try
            {
                //Open ValueNet
                NetType = ValueNet.DetectNetType("ValueNet.nnue");
                ValueNet.LoadNet("ValueNet.nnue", true);
            }
            catch
            {
            }
        }
    }
    public void ChangeThreadCount(int Count)
    {
        MainBranch = new BranchThread();
        float[,] StartMatrix = new float[45056, 520];
        Array.Copy(ValueNet.StartMatrix, StartMatrix, StartMatrix.Length);
        float[][][,] Weigths = new float[8][][,];
        Array.Copy(ValueNet.Weigths, Weigths, Weigths.Length);
        float[][][] Biases = new float[8][][];
        Array.Copy(ValueNet.Biases, Biases, Biases.Length);
        float[,] HalfkpMatrix = new float[40960, 256];
        Array.Copy(ValueNet.HalfkpMatrix, HalfkpMatrix, HalfkpMatrix.Length);
        float[] HalfkpMatrixBias = new float[512];
        Array.Copy(ValueNet.HalfkpMatrixBias, HalfkpMatrixBias, HalfkpMatrixBias.Length);
        float[][,] HalfkpWeigths = new float[3][,];
        Array.Copy(ValueNet.HalfkpWeigths, HalfkpWeigths, HalfkpWeigths.Length);
        float[][] HalfkpBiases = new float[3][];
        Array.Copy(ValueNet.HalfkpBiases, HalfkpBiases, HalfkpBiases.Length);
        Array.Copy(StartMatrix, MainBranch.treesearch.ValueNet.StartMatrix, StartMatrix.Length);
        Array.Copy(Weigths, MainBranch.treesearch.ValueNet.Weigths, Weigths.Length);
        Array.Copy(HalfkpMatrix, MainBranch.treesearch.ValueNet.HalfkpMatrix, HalfkpMatrix.Length);
        Array.Copy(Biases, MainBranch.treesearch.ValueNet.Biases, Biases.Length);
        Array.Copy(HalfkpMatrixBias, MainBranch.treesearch.ValueNet.HalfkpMatrixBias, HalfkpMatrixBias.Length);
        Array.Copy(HalfkpWeigths, MainBranch.treesearch.ValueNet.HalfkpWeigths, HalfkpWeigths.Length);
        Array.Copy(HalfkpBiases, MainBranch.treesearch.ValueNet.HalfkpBiases, HalfkpBiases.Length);
        if (Count > 1)
        {
            ThreadTreesearches = new BranchThread[Count - 1];
            for (int i = 0; i < Count - 1; i++)
            {
                ThreadTreesearches[i] = new BranchThread();
                Array.Copy(StartMatrix, ThreadTreesearches[i].treesearch.ValueNet.StartMatrix, StartMatrix.Length);
                Array.Copy(Weigths, ThreadTreesearches[i].treesearch.ValueNet.Weigths, Weigths.Length);
                Array.Copy(HalfkpMatrix, ThreadTreesearches[i].treesearch.ValueNet.HalfkpMatrix, HalfkpMatrix.Length);
                Array.Copy(Biases, ThreadTreesearches[i].treesearch.ValueNet.Biases, Biases.Length);
                Array.Copy(HalfkpMatrixBias, ThreadTreesearches[i].treesearch.ValueNet.HalfkpMatrixBias, HalfkpMatrixBias.Length);
                Array.Copy(HalfkpWeigths, ThreadTreesearches[i].treesearch.ValueNet.HalfkpWeigths, HalfkpWeigths.Length);
                Array.Copy(HalfkpBiases, ThreadTreesearches[i].treesearch.ValueNet.HalfkpBiases, HalfkpBiases.Length);
            }

        }
        else
            ThreadTreesearches = null;
    }
    public void Test(int Iterations, TrainingPosition[] Positions, bool Halfkp, bool HalfKav2)
    {
        for (int i = 0; i < Iterations; i++)
        {
            if (Halfkp)
            {
                //Backpropagate the network
                ValueNet.BackPropagationHalfkp(Positions);
                ValueNet.AdamW(new float[][][,] { ValueNet.HalfkpWeigthChanges }, new float[][][] { ValueNet.HalfkpBiasChange }, new float[][,] { ValueNet.HalfkpMatrixChange }, new float[][] { ValueNet.HalfkpMatrixBiasChange }, 0.9f , 0.9f , 0.1f , 0.22f * Iterations , Iterations , 0.28f * Iterations , 0.0001f);
                Console.WriteLine("The Error is {0}", ValueNet.CostOfHalfkpNet(Positions)[0]);
            }
            else if (HalfKav2)
            {
                //Backpropagate the network
                ValueNet.BackPropagation2(Positions, 1);
                ValueNet.setNet(new float[][][][,] { ValueNet.WeigthChanges }, new float[][][][] { ValueNet.BiasChange }, new float[][,] { ValueNet.MatrixChange }, 0.5f);
                Console.WriteLine("The Error is {0}", ValueNet.CostOfNet(Positions));
            }
        }
    }
    public void findHyperparameters(TrainingPosition[] Positions, bool Halfkp, bool HalfKav2)
    {
        float[] change = new float[3];
        for (int i = 0; i < 3; i++)
            change[i] = 0.5f;
        float current_step_size = 0.25f;
        float bestscore = 1, currentscore = 0;
        int currentchange = 0;
        bool didchange = false , update = false;
        while (true)
        {
            //do a test run
            for (int i = 0; i < 100; i++)
            {
                if (Halfkp)
                {
                    //Backpropagate the network
                    ValueNet.BackPropagationHalfkp(Positions);
                    ValueNet.AdamW(new float[][][,] { ValueNet.HalfkpWeigthChanges }, new float[][][] { ValueNet.HalfkpBiasChange }, new float[][,] { ValueNet.HalfkpMatrixChange }, new float[][] { ValueNet.HalfkpMatrixBiasChange }, change[0], change[1], change[2], 0.22f * 100, 100, 0.28f * 100, 0.0001f);
                    currentscore = (float)ValueNet.CostOfHalfkpNet(Positions)[0];
                }
            }
            //reset the neural net
            ValueNet = new NNUE();
            ValueNet.LoadNet("ValueNet.nnue", false);

            //update the best score
            if (currentscore < bestscore)
            {
                bestscore = currentscore;
                currentchange++;
                update = false;
                didchange = false;
                if(currentchange == 3)
                {
                    currentchange = 0;
                    current_step_size /= 2;
                }
            }
            else if(update)
            {
                change[currentchange] += 2 * current_step_size;
                currentchange++;
                if (currentchange == 3)
                {
                    currentchange = 0;
                    current_step_size /= 2;
                }
            }
            //write the dialog
            Console.WriteLine("The current parameters are:");
            Console.WriteLine("Learning Rate {0}", change[2]);
            Console.WriteLine("Momentum {0}", change[0]);
            Console.WriteLine("Momentum 2 {0}", change[1]);
            Console.WriteLine("the current score is {0}", currentscore);
            Console.WriteLine("the best score is {0}", bestscore);
            //make the change on the current parameter
            if (didchange)
            {
                update = true;
                change[currentchange] -= 2 * current_step_size;
            }
            else
            {
                didchange = true;
                change[currentchange] += current_step_size;
            }
        }
    }
    public void graphHyperparameters(TrainingPosition[] Positions, bool Halfkp, bool HalfKav2)
    {
        float currentBest = 0 , bestValue = 1 , currentValue = 0;
        string Output = "";
        for (int i = 200; i < 400; i++)
        {
            for (int j = 0; j < 10; j++)
            {
                //Backpropagate the network
                ValueNet.BackPropagationHalfkp(Positions);
                ValueNet.AdamW(new float[][][,] { ValueNet.HalfkpWeigthChanges }, new float[][][] { ValueNet.HalfkpBiasChange }, new float[][,] { ValueNet.HalfkpMatrixChange }, new float[][] { ValueNet.HalfkpMatrixBiasChange }, ((float)i) / 400, 0.9f, 0.1f, 22, 100, 28, 0.0001f);
            }
            currentValue = (float)ValueNet.CostOfHalfkpNet(Positions)[0];
            Output += (int)currentValue + "." + currentValue.ToString().Split(',')[1] + "\n";
            if(currentValue < bestValue)
            {
                bestValue = currentValue;
                currentBest = ((float)i) / 200;
            }
            //reset the neural net
            ValueNet = new NNUE();
            ValueNet.LoadNet("ValueNet.nnue", false);
        }
        Console.WriteLine("The best value {0} used the momentum parameter {1}", bestValue, currentBest);
        bestValue = 1;
        Output += "\n\n";
        for (int i = 200; i < 400; i++)
        {
            for (int j = 0; j < 10; j++)
            {
                //Backpropagate the network
                ValueNet.BackPropagationHalfkp(Positions);
                ValueNet.AdamW(new float[][][,] { ValueNet.HalfkpWeigthChanges }, new float[][][] { ValueNet.HalfkpBiasChange }, new float[][,] { ValueNet.HalfkpMatrixChange }, new float[][] { ValueNet.HalfkpMatrixBiasChange }, 0.9f, ((float)i) / 400, 0.1f, 22, 100, 28, 0.0001f);
            }
            currentValue = (float)ValueNet.CostOfHalfkpNet(Positions)[0];
            Output += (int)currentValue + "." + currentValue.ToString().Split(',')[1] + "\n";
            if (currentValue < bestValue)
            {
                bestValue = currentValue;
                currentBest = ((float)i) / 200;
            }
            //reset the neural net
            ValueNet = new NNUE();
            ValueNet.LoadNet("ValueNet.nnue", false);
        }
        Console.WriteLine("The best value {0} used the velocity parameter {1}", bestValue, currentBest);
        bestValue = 1;
        Output += "\n\n";
        for (int i = 1; i < 201; i++)
        {
            for (int j = 0; j < 10; j++)
            {
                //Backpropagate the network
                ValueNet.BackPropagationHalfkp(Positions);
                ValueNet.AdamW(new float[][][,] { ValueNet.HalfkpWeigthChanges }, new float[][][] { ValueNet.HalfkpBiasChange }, new float[][,] { ValueNet.HalfkpMatrixChange }, new float[][] { ValueNet.HalfkpMatrixBiasChange }, 0.9f, 0.9f, ((float)i) / 4020, 22, 100, 28, 0.0001f);
            }
            currentValue = (float)ValueNet.CostOfHalfkpNet(Positions)[0];
            Output += (int)currentValue + "." + currentValue.ToString().Split(',')[1] + "\n";
            if (currentValue < bestValue)
            {
                bestValue = currentValue;
                currentBest = ((float)i) / 200;
            }
            //reset the neural net
            ValueNet = new NNUE();
            ValueNet.LoadNet("ValueNet.nnue", false);
        }
        Console.WriteLine("The best value {0} used the learning rate parameter {1}", bestValue, currentBest);
        bestValue = 1;
        Output += "\n\n";
        for (int i = 1; i < 201; i++)
        {
            for (int j = 0; j < 10; j++)
            {
                //Backpropagate the network
                ValueNet.BackPropagationHalfkp(Positions);
                ValueNet.AdamW(new float[][][,] { ValueNet.HalfkpWeigthChanges }, new float[][][] { ValueNet.HalfkpBiasChange }, new float[][,] { ValueNet.HalfkpMatrixChange }, new float[][] { ValueNet.HalfkpMatrixBiasChange }, 0.9f, 0.9f, 0.1f, 22, 100, 28, ((float)i) / 20100);
            }
            currentValue = (float)ValueNet.CostOfHalfkpNet(Positions)[0];
            Output += (int)currentValue + "." + currentValue.ToString().Split(',')[1] + "\n";
            if (currentValue < bestValue)
            {
                bestValue = currentValue;
                currentBest = ((float)i) / 20000;
            }
            //reset the neural net
            ValueNet = new NNUE();
            ValueNet.LoadNet("ValueNet.nnue", false);
        }
        Console.WriteLine("The best value {0} used the weight decay parameter {1}", bestValue, currentBest);
        Console.WriteLine("Done !");
        StreamWriter sw = new StreamWriter("Errors.txt");
        sw.WriteLine(Output);
        sw.Flush();
        sw.Close();
        while (true) { }
    }
    public void SetNet(string File)
    {
        ValueNet.LoadNet(File , true);
    }
    public MCTSimOutput MonteCarloTreeSim(byte[,] InputBoard, byte color, int NodeCount, bool NewTree, bool Random, bool NNUE , bool Halfkp , bool HalfKav2, float c_puct)
    {
        MCTSimOutput Output = new MCTSimOutput();

        if (NewTree)
            CurrentTree = GetTree(NodeCount, false, NNUE, Halfkp, HalfKav2, new Node(InputBoard, color, null, 0), false, c_puct).Tree;
        else
            CurrentTree = GetTree(NodeCount, false, NNUE, Halfkp, HalfKav2, CurrentTree, false, c_puct).Tree;
        int BestscorePlace = 0;

        List<double> Values = new List<double>();

        foreach (Node node in CurrentTree.ChildNodes)
            Values.Add(node.Denominator);

        if (Random)
            BestscorePlace = RandomWeightedChooser(Values);
        else
            BestscorePlace = BestNumber(Values);

        if (BestscorePlace >= CurrentTree.ChildNodes.Count)
        {
            Console.WriteLine("error!");
        }

        Output.Position = MoveGenerator.PlayMove(CurrentTree.Board, CurrentTree.ChildNodes[BestscorePlace].Color, CurrentTree.ChildNodes[BestscorePlace].Move);
        Output.eval = CurrentTree.Numerator / CurrentTree.Denominator;
        CurrentTree = CurrentTree.ChildNodes.ToArray()[BestscorePlace];
        CurrentTree.Board = new byte[9, 9];
        Array.Copy(Output.Position, CurrentTree.Board, Output.Position.Length);
        return Output;
    }
    public int[][] MultithreadMcts(byte[,] InputBoard, byte color, int NodeCount, bool NNUE, bool Halfkp , bool HalfKav2 , int ThreadCount , bool Infinite , bool UseTime , long Time , float c_puct)
    {
        int[][] Output = new int[2][];
        if (ThreadTreesearches == null && ThreadCount - 1 != 0 || ThreadTreesearches != null && ThreadTreesearches.Length + 1 != ThreadCount)
            ChangeThreadCount(ThreadCount);
        if (ThreadCount < 1)
            ThreadCount = 1;
        int NodeAmount = NodeCount;
        float AverageDepth = 0, Seldepth = 0;
        if (UseTime)
            ThreadCount--;
        int[] BranchesPerThread = new int[ThreadCount];
        if (UseTime)
            ThreadCount++;
        Thread[] ThreadPool = new Thread[ThreadCount - 1];
        if (UseTime)
            ThreadCount--;
        Stopwatch sw = new Stopwatch();
        bool NotFinished = true;
        bool TimeIsUp = false;
        sw.Start();
        Node Tree;
        TreesearchOutput[] OutputArray;
        TreesearchOutput TreeGenOut = new TreesearchOutput();
        if (MoveGenerator.Mate(InputBoard, color) != 2)
        {
            int score = MoveGenerator.Mate(InputBoard, color);
            if (score == 50)
                score = -1;
            else if (score == -50)
                score = 1;
            else
                score = 0;

            Console.WriteLine("info depth 0 score mate {0}", score);
            return null;
        }
        if (CurrentTree != null && CompareBoards(CurrentTree.Board, InputBoard) && color == CurrentTree.Color)
        {
            Tree = CurrentTree;
        }
        else
        {
            if (stop)
                stop = false;
            //Generate the first Layer
            TreeGenOut = GetTree(1, false, NNUE, Halfkp, HalfKav2, new Node(InputBoard, color, null, -2) , false , c_puct);

            Seldepth = TreeGenOut.Seldepth;
            Tree = TreeGenOut.Tree;
            AverageDepth += TreeGenOut.averageDepth * Tree.Denominator;
        }

        //Multithreaded Threadgen
        //Basic Variables
        int TreeAmount = Tree.ChildNodes.Count;
        BranchThreadInput[] Branches = new BranchThreadInput[TreeAmount];
        if (ThreadCount > TreeAmount)
            ThreadCount = TreeAmount;
        OutputArray = new TreesearchOutput[TreeAmount];
        for (int i = 0; i < ThreadCount; i++)
        {
            if (TreeAmount % ThreadCount > i)
                BranchesPerThread[i] = TreeAmount / ThreadCount + 1;            
            else
                BranchesPerThread[i] = TreeAmount / ThreadCount;
        }
        if (!(Tree.Denominator >= NodeAmount))
        {
            //init
            for (int i = 0; i < TreeAmount; i++)
                Branches[i] = new BranchThreadInput(Tree.ChildNodes[i], 0, NNUE);
            if (UseTime)
                ThreadCount++;
            //Leveling
            int Counter = 0;
            for (int i = 0; i < ThreadCount; i++)
            {
                if (i == ThreadCount - 1 && !UseTime)
                {
                    MainBranch.c_puct = c_puct;
                    MainBranch.Infinite = Infinite;
                    MainBranch.NNUE = NNUE;
                    MainBranch.HalfKav2 = HalfKav2;
                    MainBranch.HalfKp = Halfkp;
                    MainBranch.Nodecount = NodeCount / ThreadCount;
                    MainBranch.Outputs = new TreesearchOutput[BranchesPerThread[i]];
                    MainBranch.BranchNumber = new int[BranchesPerThread[i]];
                    MainBranch.Trees = new Node[BranchesPerThread[i]];
                    for (int j = 0; j < BranchesPerThread[i]; j++)
                    {
                        MainBranch.BranchNumber[j] = Counter;
                        MainBranch.Trees[j] = Branches[Counter].Tree;
                        MainBranch.Trees[j].Board = new byte[9, 9];
                        Array.Copy(Tree.Board, MainBranch.Trees[j].Board, Tree.Board.Length);
                        MainBranch.Trees[j].Board = MoveGenerator.PlayMove(MainBranch.Trees[j].Board, MainBranch.Trees[j].Color, MainBranch.Trees[j].Move);
                        Counter++;
                    }
                    MainBranch.UpdateInfinite(InputBoard, color);
                    MainBranch.StartThread();
                }
                else if (i == ThreadCount - 1 && UseTime)
                {
                    sw = new Stopwatch();
                    sw.Start();
                    while(sw.ElapsedMilliseconds < Time && !stop)
                    {
                        //Do nothing
                        TimeIsUp = true;
                    }
                }
                else
                {
                    ThreadTreesearches[i].c_puct = c_puct;
                    ThreadTreesearches[i].Infinite = Infinite;
                    ThreadTreesearches[i].NNUE = NNUE;
                    ThreadTreesearches[i].HalfKp = Halfkp;
                    ThreadTreesearches[i].HalfKav2 = HalfKav2;
                    ThreadTreesearches[i].Nodecount = NodeCount / ThreadCount;
                    ThreadTreesearches[i].Outputs = new TreesearchOutput[BranchesPerThread[i]];
                    ThreadTreesearches[i].BranchNumber = new int[BranchesPerThread[i]];
                    ThreadTreesearches[i].Trees = new Node[BranchesPerThread[i]];
                    for (int j = 0; j < BranchesPerThread[i]; j++)
                    {
                        ThreadTreesearches[i].BranchNumber[j] = Counter;
                        ThreadTreesearches[i].Trees[j] = Branches[Counter].Tree;
                        ThreadTreesearches[i].Trees[j].Board = new byte[9, 9];
                        Array.Copy(Tree.Board, ThreadTreesearches[i].Trees[j].Board, Tree.Board.Length);
                        ThreadTreesearches[i].Trees[j].Board = MoveGenerator.PlayMove(ThreadTreesearches[i].Trees[j].Board, ThreadTreesearches[i].Trees[j].Color, ThreadTreesearches[i].Trees[j].Move);
                        Counter++;
                    }
                    ThreadTreesearches[i].UpdateInfinite(InputBoard, color);
                    ThreadPool[i] = new Thread(ThreadTreesearches[i].StartThread);
                    ThreadPool[i].Start();
                }
            }
            if (MainBranch.treesearch.wasStopped || TimeIsUp)
            {
                for (int i = 0; i < ThreadCount - 1; i++)
                    ThreadTreesearches[i].Stop();
                wasStopped = false;
                MainBranch.treesearch.wasStopped = false;
            }
            //wait
            while (NotFinished)
            {
                NotFinished = false;
                for (int i = 0; i < ThreadCount - 1; i++)
                    if (ThreadPool[i].IsAlive)
                        NotFinished = true;
            }

            //recompile the different Outputs
            for (int i = 0; i < ThreadCount - 1; i++)
                for (int j = 0; j < ThreadTreesearches[i].BranchNumber.Length; j++)
                    OutputArray[ThreadTreesearches[i].BranchNumber[j]] = ThreadTreesearches[i].Outputs[j];

            if (!UseTime)
                for (int i = 0; i < MainBranch.BranchNumber.Length; i++)
                    OutputArray[MainBranch.BranchNumber[i]] = MainBranch.Outputs[i];

                //analyze the Branches
                for (int i = 0; i < TreeAmount; i++)
                {
                    Tree.ChildNodes[i] = OutputArray[i].Tree;
                    if (OutputArray[i].Seldepth > Seldepth - 1)
                        Seldepth = OutputArray[i].Seldepth + 1;
                    AverageDepth += OutputArray[i].averageDepth;
                    Tree.Denominator += Tree.ChildNodes[i].Denominator;
                Tree.Numerator -= Tree.ChildNodes[i].Numerator;
                }
                //Output first Info
                Console.WriteLine("info depth {0} seldepth {1} time {4} nodes {5} score cp {2} nps {3}", (int)(AverageDepth / (Tree.Denominator + 0.000000001)) + 1, Seldepth + 1, -Math.Round(CentipawnLoss(Tree.Numerator / Tree.Denominator)), (int)(((float)Tree.Denominator / (sw.ElapsedMilliseconds + 1)) * 1000), sw.ElapsedMilliseconds, Tree.Denominator);
        }
        //Get the Output(the next node with the Biggest Average score) from the tree
        for (int i = 0; i < 2; i++)
        {
            int Counter = 0;
            int BestscorePlace = -1;
            double BestScore = -400;
            double CurrentScore;
            foreach (Node node in Tree.ChildNodes)
            {
                CurrentScore = node.Numerator / (node.Denominator + 0.00000001);
                if (CurrentScore > BestScore)
                {
                    BestScore = CurrentScore;
                    BestscorePlace = Counter;
                }
                Counter++;
            }
            if (BestscorePlace >= 0)
            {
                Output[i] = new int[Tree.ChildNodes.ToArray()[BestscorePlace].Move.Length];
                Array.Copy(Tree.ChildNodes.ToArray()[BestscorePlace].Move, Output[i], Output[i].Length);
            }
            else
            {
                Output[i] = new int[0];
            }
            if (color == 1)
                color = 0;
            else
                color = 1;
            if (Tree.ChildNodes.Count != 0)
                Tree = Tree.ChildNodes[BestscorePlace];
        }

        return Output;
    }
    public float MonteCarloRollout(byte[,] Board, byte Color, int maxLength)
    {     
        float Score = 2;
        byte[,] currentBoard = new byte[9, 9];
        Array.Copy(Board, currentBoard, Board.Length);
        byte currentColor = Color;
        for (int i = 0; i < maxLength; i++)
        {
            Score = MoveGenerator.Mate(currentBoard, currentColor);
            if (Score == 2)
            {
                currentBoard = RandomSim(currentBoard, currentColor);
                if (currentColor == 1)
                    currentColor = 0;
                else
                    currentColor = 1;
            }
            else
                return ScoreInverse(Score, currentColor);
        }
        Score = MoveGenerator.Mate(currentBoard, currentColor);
        if (Score == 2)
            return 0;
        else
            return ScoreInverse(Score,currentColor);
        
    }
    public float ScoreInverse(float Score, byte Color)
    {
        if (Color == 1)
        {
            if (Score == 1)
                Score = -1;
            else if (Score == -1)
                Score = 1;
        }
        return Score;
    }
    public byte[,] RandomSim(byte[,] InputBoard, byte color)
    {
        List<int[]> Moves = MoveGenerator.ReturnPossibleMoves(InputBoard, color);
        bool finished = false;
        byte otherColor = 0;
        if (color == 0)
            otherColor = 1;
        while (!finished)
        {
            int Place = random.Next(Moves.Count - 1);
            InputBoard = MoveGenerator.PlayMove(InputBoard, color, Moves[Place]);
            int[] MoveUnmake = MoveGenerator.UnmakeMove;

            if (!MoveGenerator.CompleteCheck(InputBoard, otherColor))
                return InputBoard;
            else
                InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUnmake);
        }
        return InputBoard;
    }
    public int BestNumber(List<double> Input)
    {
        int counter = 0;
        int bestplace = 0;
        double bestscore = -1;

        foreach (double Value in Input)
        {
            if (Value > bestscore)
            {
                bestplace = counter;
                bestscore = Value;
            }
            counter++;
        }

        return bestplace;
    }
    public int RandomWeightedChooser(List<double> Input)
    {
        int counter = 0;
        double Denominator = 0;
        double LastValue = 0;

        foreach (double Value in Input)
        {
            Denominator += (Value + 1) / 2;
            if (Value == 1)
                return counter;

            counter++;
        }

        double[] ValueList = new double[counter];
        counter = 0;

        foreach (double Value in Input)
        {
            ValueList[counter] = (Value + 1) / 2 + LastValue;
            LastValue = ValueList[counter];
            counter++;
        }

        double RamdomDouble = random.NextDouble();

        for (int i = 0; i < ValueList.Length; i++)
            if (RamdomDouble < ValueList[i] / Denominator)
                return i;

        return 0;
    }
    public byte[][,] PlayGameFromMooves(byte[,] InputBoard, byte color, int[][] Mooves , bool TreeUpdate)
    {
        for (int i = 0; i < Mooves.Length; i++)
        {
            InputBoard = MoveGenerator.PlayMove(InputBoard, color, Mooves[i]);
            if (color == 1)
                color = 0;
            else
                color = 1;
            if(TreeUpdate && CurrentTree != null)
            {
                bool Change = false;
                for (int l = 0; l < CurrentTree.ChildNodes.Count; l++)
                    if (CurrentTree.ChildNodes[l].Board != null && CompareBoards(InputBoard, CurrentTree.ChildNodes[l].Board))
                    {
                        Change = true;
                        CurrentTree = CurrentTree.ChildNodes[l];
                        break;
                    }
                if (!Change)
                    CurrentTree = null;
            }
        }
        byte[,] ColorOut = new byte[1, 1];
        ColorOut[0, 0] = color;
        return new byte[2][,] { InputBoard, ColorOut };
    }
    public TreesearchOutput GetTree(long NodeCount, bool Output, bool NNUE, bool Halfkp, bool HalfKav2, Node startNode, bool Infinite , float c_puct)
    {
        Node Tree = startNode;
        NodeCount -= (long)startNode.Denominator;
        Node CurrentNode;
        List<Node> Path = new List<Node>();
        double BestScore = -2;
        byte[,] CurrentBoard = new byte[9, 9];
        Array.Copy(startNode.Board, CurrentBoard, CurrentBoard.Length);
        int BestscorePlace = 0;
        double CurrentScore = 0;
        int Counter = 0;
        float CurrentNumerator = 0;
        double depth = 0;
        byte[,] BoardCopy = new byte[9, 9];
        int seldepth = 0;
        int averageDepth = 0;
        long outputCount = 4096;
        if (NodeCount < 2)
            NodeCount = 2;
        if (NodeCount / 10 < 4096 && NodeCount > 10)
            outputCount = NodeCount / 10;
        //init stopwatch for nps
        Stopwatch sw = new Stopwatch();
        //start it
        sw.Start();


        //Update the tree with N None Leaf Nodes
        for (int i = 0; i < NodeCount; i++)
        {
            if (Infinite)
                i = 20;
            CurrentNode = Tree;
            Array.Copy(CurrentNode.Board, CurrentBoard, CurrentBoard.Length);
            //Find a Leaf Node
            //While the CurrentNode is Not a Leaf Node Continue
            depth = 0;
            while (!CurrentNode.IsALeafNode)
            {
                //Find Best Node 
                Counter = 0;
                foreach (Node node in CurrentNode.ChildNodes)
                {
                    CurrentScore = node.GetScore(CurrentNode.Denominator , c_puct);
                    if (CurrentScore > BestScore)
                    {
                        BestScore = CurrentScore;
                        BestscorePlace = Counter;
                    }
                    Counter++;
                }

                //Add the current Node to the Path
                CurrentNode.NextNodePlace = BestscorePlace;
                Path.Insert(0, CurrentNode);

                //go to Next Node
                CurrentNode = CurrentNode.ChildNodes.ToArray()[BestscorePlace];
                //Play the Move associated to the position on the Board
                CurrentBoard = MoveGenerator.PlayMove(CurrentBoard, CurrentNode.Color, CurrentNode.Move);
                //Reset the Best Score
                BestScore = -2;
                BestscorePlace = 0;
                //Increment the depth
                depth++;
            }
            //if the current Node Already has a Value
            if (CurrentNode.Denominator != 0 && !CurrentNode.End)
            {
                //Expand The Current Node
                List<int[]> ChildNodeMoves = MoveGenerator.ReturnPossibleMoves(CurrentBoard, CurrentNode.Color);
                Array.Copy(CurrentBoard, BoardCopy, CurrentBoard.Length);
                float Predictor = 1;
                byte OtherColor = 0;
                bool End = true;
                if (CurrentNode.Color == 0)
                    OtherColor = 1;
                Counter = 0;
                foreach (int[] Move in ChildNodeMoves) 
                {
                    CurrentBoard = MoveGenerator.PlayMove(CurrentBoard, CurrentNode.Color, Move);
                    if (!MoveGenerator.CompleteCheck(CurrentBoard, OtherColor))
                    {
                        End = false;
                        Predictor = (eval.PestoEval(CurrentBoard, OtherColor) + 1) / 2;
                        CurrentNode.ChildNodes.Add(new Node(null, OtherColor, Move , Predictor));
                        if (CurrentNode.ChildNodes[Counter].Probability > BestScore)
                        {
                            BestScore = CurrentNode.ChildNodes[Counter].Probability;
                            BestscorePlace = Counter;
                        }
                        Counter++;
                    }
                    //CurrentBoard = UndoMove(CurrentBoard, UnmakeMove);
                    Array.Copy(BoardCopy, CurrentBoard, CurrentBoard.Length);
                }
                if (!End)
                {
                    if (i == 1 && CurrentNode.ChildNodes.Count > NodeCount)
                        NodeCount = CurrentNode.ChildNodes.Count + 1;
                    int place = BestscorePlace;
                    BestScore = -2;
                    BestscorePlace = 0;
                    CurrentNode.IsALeafNode = false;

                    CurrentBoard = MoveGenerator.PlayMove(CurrentBoard, CurrentNode.Color, CurrentNode.ChildNodes.ToArray()[place].Move);
                    //Update the Numerator and Denominator
                    if (NNUE)
                    {
                        if (HalfKav2)
                            CurrentNumerator = ValueNet.UseNet(CurrentBoard, OtherColor);
                        else if (Halfkp)
                            CurrentNumerator = ValueNet.UseHalfkpNet(CurrentBoard, OtherColor);
                    }
                    else
                        CurrentNumerator = eval.PestoEval(CurrentBoard, OtherColor);

                    Array.Copy(BoardCopy, CurrentBoard, CurrentBoard.Length);

                    CurrentNode.ChildNodes.ToArray()[place].Numerator = (float)CurrentNumerator;
                    CurrentNode.ChildNodes.ToArray()[place].Denominator++;

                    CurrentNumerator = -CurrentNumerator;
                    CurrentNode.Numerator += (float)CurrentNumerator;
                    CurrentNode.Denominator++;
                }
                else
                {
                    CurrentNode.Denominator++;
                    CurrentNode.MateValue = MoveGenerator.Mate(CurrentBoard, CurrentNode.Color);
                    CurrentNumerator = CurrentNode.MateValue;
                    CurrentNode.Numerator = 2 * CurrentNode.MateValue;
                    CurrentNode.End = true;
                }
            }
            else if(CurrentNode.End)
            {
                CurrentNode.Denominator++;
                CurrentNumerator = CurrentNode.MateValue;
                CurrentNode.Numerator += CurrentNode.MateValue;
            }
            else
            {
                //Update the Numerator and Denominator
                if (NNUE)
                {
                    if(HalfKav2)
                      CurrentNumerator = ValueNet.UseNet(CurrentBoard, CurrentNode.Color);
                    else if (Halfkp)
                        CurrentNumerator = ValueNet.UseHalfkpNet(CurrentBoard, CurrentNode.Color);
                }
                else
                    CurrentNumerator = eval.PestoEval(CurrentBoard, CurrentNode.Color);

                CurrentNode.Numerator = CurrentNumerator;
                CurrentNode.Denominator++;
            }
            //Backpropagate the Node
            foreach (Node node in Path)
            {
                CurrentNumerator = -CurrentNumerator;
                //Update the Parent 
                node.ChildNodes.ToArray()[node.NextNodePlace] = CurrentNode;
                node.Denominator++;
                node.Numerator += CurrentNumerator;

                //Set Current Node to parent
                CurrentNode = node;
            }

            //Set the tree to the current Node
            Tree = CurrentNode;
            //Reset the Path
            Path = new List<Node>();
            if (depth > seldepth)
                seldepth = (int)depth;
            averageDepth += (int)depth;

            if (Output && i % outputCount == 0)
            {
                Console.WriteLine("info depth {0} seldepth {1} time {4} nodes {5} score cp {2} nps {3}", averageDepth / (i + 1), seldepth, -Math.Round(CentipawnLoss(Tree.Numerator / Tree.Denominator)), (int)(((float)i / (sw.ElapsedMilliseconds + 1)) * 1000), sw.ElapsedMilliseconds, Tree.Denominator);
            }

            StopSemaphore.WaitOne();
            if (stop)
            {
                stop = false;
                wasStopped = true;
                break;
            }
            StopSemaphore.Release();
        }
        CurrentTree = Tree;
        TreesearchOutput FunctionOutput = new TreesearchOutput();

        FunctionOutput.Tree = Tree;
        FunctionOutput.Seldepth = seldepth;
        FunctionOutput.averageDepth = averageDepth;

        return FunctionOutput;
    }
    public void SetStop(bool Input)
    {
        StopSemaphore.WaitOne();
        stop = Input;
        if (MainBranch != null)
            MainBranch.Stop();
        StopSemaphore.Release();
    }
    public double CentipawnLoss(double Input)
    {
        return Input * 800;
    }
    public bool CompareBoards(byte[,] Board1 , byte[,] Board2)
    {
        for(int i = 0; i < 9; i++)
        {
            for (int j = 0; j < 9; j++)
            {
                if (Board1[i, j] != Board2[i, j])
                    return false;
            }
        }
        return true;
    }
    public List<int[]> OrderMoves(List <int[]> Moves , byte[,] InputBoard , byte color)
    {
        byte OtherColor = 0;
        if (color == 0)
            OtherColor = 1;
        bool done = false;
        int CurrentIndex = 0;
        int[] MoveUndo;
        List<float> MoveValues = new List<float>();
        List<int[]> MoveList = new List<int[]>();
        foreach(int[] Move in Moves)
        {
            InputBoard = MoveGenerator.PlayMove(InputBoard, color, Move);
            MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
            Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);
            if (!MoveGenerator.CompleteCheck(InputBoard, OtherColor)) 
            {
                float currentValue = eval.PestoEval(InputBoard, OtherColor);
                if (CurrentIndex == 0)
                {
                    MoveValues.Add(currentValue);
                    MoveList.Add(Moves[0]);
                }
                else
                {
                    for (int i = 0; i < MoveValues.Count; i++)
                    {
                        if (MoveValues[i] < currentValue)
                        {
                            MoveValues.Insert(i, currentValue);
                            MoveList.Insert(i, Moves[CurrentIndex]);
                            done = true;
                            break;
                        }
                    }
                    if(!done)
                    {
                        MoveValues.Add(currentValue);
                        MoveList.Add(Moves[CurrentIndex]);
                    }
                    done = false;
                }
            }
            InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);
            CurrentIndex++;
        }
        return MoveList;
    }
    public int[] MinMaxAlphaBeta(byte[,] InputBoard, byte color, int depthPly, bool NNUE, bool Halfkp, bool HalfKav2)
    {
        int[] Output = new int[0];
        int[] MoveUndo;
        List<int[]> Moves = MoveGenerator.ReturnPossibleMoves(InputBoard, color );
        List<int[]> CleanedMoves = new List<int[]>();
        foreach (int[] Move in Moves)
        {
            if (Move.Length != 5 || !MoveGenerator.CastlingCheck(InputBoard, Move))
                CleanedMoves.Add(Move);
        }
        CleanedMoves = OrderMoves(CleanedMoves, InputBoard, color);
        double BestScore = 0, CurrentScore = -2;
        byte Othercolor = 0;
        int counter = 0;

        if (color == 0)
            Othercolor = 1;

        if (depthPly <= 1)
        {
            foreach (int[] Move in CleanedMoves)
            {
                InputBoard = MoveGenerator.PlayMove(InputBoard, color, Move);
                MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
                Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);
                int MatingValue = MoveGenerator.Mate(InputBoard, Othercolor);

                if (NNUE)
                {
                    if (HalfKav2)
                        CurrentScore = ValueNet.UseNet(InputBoard, Othercolor);
                    else if (Halfkp)
                        CurrentScore = ValueNet.UseHalfkpNet(InputBoard, Othercolor);
                }
                else
                    CurrentScore = eval.PestoEval(InputBoard, Othercolor);

                if (counter == 0)
                {
                    Output = CleanedMoves.ToArray()[counter];
                    BestScore = CurrentScore;
                }
                else
                {
                    if (BestScore < CurrentScore)
                    {
                        Output = CleanedMoves.ToArray()[counter];
                        BestScore = CurrentScore;
                    }
                }

                if (MatingValue != 2)
                {
                    CurrentScore = MatingValue;
                    if (BestScore < CurrentScore)
                    {
                        Output = CleanedMoves.ToArray()[counter];
                        BestScore = CurrentScore;
                    }
                }
                InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);
                counter++;
                Nodecount++;
            }
        }
        else
        {
            foreach (int[] Move in CleanedMoves)
            {
                InputBoard = MoveGenerator.PlayMove(InputBoard, color, Move);
                MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
                Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);
                int MatingValue = MoveGenerator.Mate(InputBoard, Othercolor);

                if (MatingValue == 2)
                {
                    if (CurrentScore == -2)
                    {
                        CurrentScore = -MinMaxAlphaBetaScore(InputBoard, Othercolor, depthPly - 1, BestScore, false, NNUE, Halfkp, HalfKav2);
                        if (CurrentScore != -2)
                        {
                            Output = CleanedMoves.ToArray()[counter];
                            BestScore = CurrentScore;
                        }
                    }
                    else
                    {
                        CurrentScore = -MinMaxAlphaBetaScore(InputBoard, Othercolor, depthPly - 1, BestScore, true, NNUE, Halfkp, HalfKav2);
                        if (BestScore < CurrentScore)
                        {
                            Output = CleanedMoves.ToArray()[counter];
                            BestScore = CurrentScore;
                        }
                    }
                }

                else if (MatingValue != 2)
                {
                    CurrentScore = MatingValue;
                    if (BestScore < CurrentScore)
                    {
                        Output = CleanedMoves.ToArray()[counter];
                        BestScore = CurrentScore;
                    }
                }
                InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);
                counter++;
            }
        }
        Console.WriteLine("info nodes {1} score cp {0}", Math.Round(BestScore * 100) , Nodecount);
        Nodecount = 0;
        return Output;
    }
    public double MinMaxAlphaBetaScore(byte[,] InputBoard, byte color, int depthPly, double LastBest, bool Activation, bool NNUE, bool Halfkp, bool HalfKav2)
    {
        List<int[]> Moves = MoveGenerator.ReturnPossibleMoves(InputBoard, color);
        List<int[]> CleanedMoves = new List<int[]>();
        if (Moves != null)
        {
            foreach (int[] Move in Moves)
            {
                if (Move.Length != 5 || !MoveGenerator.CastlingCheck(InputBoard, Move))
                    CleanedMoves.Add(Move);
            }
        }
        else
        {
            return 2;
        }
        double BestScore = -2, CurrentScore = 0;
        byte Othercolor = 0;
        int counter = 0;
        int[] MoveUndo;

        if (color == 0)
            Othercolor = 1;

        if (depthPly <= 1)
        {
            foreach (int[] Move in CleanedMoves)
            {

                InputBoard = MoveGenerator.PlayMove(InputBoard, color, Move);
                MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
                Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);
                if (!MoveGenerator.CompleteCheck(InputBoard, Othercolor))
                {
                    if (NNUE)
                    {
                        if (HalfKav2)
                            CurrentScore = ValueNet.UseNet(InputBoard, Othercolor);
                        else if (Halfkp)
                            CurrentScore = ValueNet.UseHalfkpNet(InputBoard, Othercolor);
                    }
                    else
                        CurrentScore = eval.PestoEval(InputBoard, Othercolor);

                    if (counter == 0)
                        BestScore = CurrentScore;

                    else if (CurrentScore > BestScore)
                        BestScore = CurrentScore;

                    counter++;
                    Nodecount++;
                    InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);
                    if (Activation && -BestScore < LastBest && counter != 0)
                        return BestScore;
                }
                else
                    InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);
            }
        }
        else
        {
            CleanedMoves = OrderMoves(CleanedMoves, InputBoard, color);
            foreach (int[] Move in CleanedMoves)
            {
                InputBoard = MoveGenerator.PlayMove(InputBoard, color, Move);
                MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
                Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);
                if (counter == 0)
                {
                    CurrentScore = -MinMaxAlphaBetaScore(InputBoard, Othercolor, depthPly - 1, BestScore, false, NNUE, Halfkp, HalfKav2);
                    if (CurrentScore == 2)
                    {
                        InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);
                        return MoveGenerator.Mate(InputBoard, Othercolor);
                    }
                    else
                        InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);
                    if (CurrentScore != -2)
                    {
                        BestScore = CurrentScore;
                        counter++;
                    }
                }
                else
                {
                    CurrentScore = -MinMaxAlphaBetaScore(InputBoard, Othercolor, depthPly - 1, BestScore, true, NNUE, Halfkp, HalfKav2);
                    if (CurrentScore == 2)
                    {
                        InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);
                        return MoveGenerator.Mate(InputBoard, Othercolor);
                    }
                    else
                        InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);
                    if (BestScore < CurrentScore)
                        BestScore = CurrentScore;
                }
                if (Activation && -BestScore < LastBest && counter != 0)
                    return BestScore;


            }
        }

        return BestScore;
    }
    public int PossiblePositionCounter(byte[,] board, int depthPly, byte color)
    {
        List<int[]> Moves = MoveGenerator.ReturnPossibleMoves(board, color);
        int[] MoveUndo;
        byte newcolor = 0;
        if (color == 0)
            newcolor = 1;
        int OutputNumber = 0;
        if (Moves == null)
            return 0;
        else
        {
            if (depthPly == 1)
            {
                OutputNumber = Moves.Count;
                foreach (int[] Move in Moves)
                {
                    if (Move.Length != 5 || !MoveGenerator.CastlingCheck(board, Move))
                    {
                        board = MoveGenerator.PlayMove(board, color, Move);
                        MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
                        Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);
                        if (MoveGenerator.CompleteCheck(board, newcolor))
                            OutputNumber--;
                        board = MoveGenerator.UndoMove(board, MoveUndo);
                    }
                    else
                        OutputNumber--;
                }
            }
            else if (depthPly < 1)
            {
                return 1;
            }
            else
            {
                foreach (int[] Move in Moves)
                {
                    if (Move.Length != 5 || !MoveGenerator.CastlingCheck(board, Move))
                    {
                        board = MoveGenerator.PlayMove(board, color, Move);
                        int[] Moveunmake = new int[MoveGenerator.UnmakeMove.Length];
                        Array.Copy(MoveGenerator.UnmakeMove, Moveunmake, MoveGenerator.UnmakeMove.Length);
                        OutputNumber += PossiblePositionCounter(board, depthPly - 1, newcolor);
                        board = MoveGenerator.UndoMove(board, Moveunmake);
                    }
                }
            }
            return OutputNumber;
        }
    }
}
class Node
{
    public float Probability;
    public int[] Move; 
    public byte Color = 0;
    public byte[,] Board;
    public float Numerator = 0;
    public float Denominator = 0;
    public List<Node> ChildNodes = new List<Node>();
    public bool IsALeafNode = true;
    public int NextNodePlace = 0;
    public bool End = false;
    public float MateValue = -2;
    public Node(byte[,] InputBoard, byte color, int[] PlayedMove, float Predictor)
    {
        Probability = Predictor;
        Move = PlayedMove;
        Color = color;
        Board = InputBoard;
    }
    public double GetScore(double FatherDenominator , float c_puct)
    {
        return (Numerator / (Denominator + 0.0000000001)) + c_puct * Probability * (Math.Sqrt(FatherDenominator) / (Denominator + 0.00000000001));
    }
}
class TreesearchOutput
{
    public Node Tree;
    public int Seldepth = 0, averageDepth = 0;
}
class MCTSimOutput
{
    public byte[,] Position;
    public float eval;
}
class BranchThreadInput
{
    public BranchThreadInput(Node InputTree , int AmountofNodes , bool NNUE)
    {
        Tree = InputTree;
        NodeAmount = AmountofNodes;
        UseNNUE = NNUE;
    }
    public Node Tree;
    public int NodeAmount;
    public bool UseNNUE;
}
class BranchThread
{
    public Treesearch treesearch = new Treesearch(1, false, 1);
    public int[] BranchNumber;
    public float c_puct = 1;
    public int Nodecount = 0;
    public bool NNUE = false;
    public bool HalfKp = false;
    public bool HalfKav2 = true;
    public bool Infinite = false;
    public Node[] Trees;
    Node ReplacementRoot;
    public TreesearchOutput[] Outputs;
    TreesearchOutput Startoutput;
    public void Stop()
    {
        treesearch.SetStop(true);
    }
    public void UpdateInfinite(byte[,] InputBoard, byte PositionColor)
    {
        ReplacementRoot = new Node(InputBoard, PositionColor, new int[0], 0);
        ReplacementRoot.IsALeafNode = false;

        foreach (Node Tree in Trees)
        { 
            ReplacementRoot.ChildNodes.Add(Tree);
            ReplacementRoot.Denominator += Tree.Denominator;
            ReplacementRoot.Numerator -= Tree.Numerator;
        }
    }
    public void StartThread()
    {

        treesearch.wasStopped = false;
        int counter = 0;
        treesearch.StopSemaphore = new Semaphore(1, 1);
        Startoutput = treesearch.GetTree(Nodecount, false, NNUE, HalfKp, HalfKav2, ReplacementRoot, Infinite, c_puct);
        foreach (Node ChildNode in Startoutput.Tree.ChildNodes)
        {
            Outputs[counter] = new TreesearchOutput();
            if (counter == 0)
            {
                Outputs[counter].Tree = ChildNode;
                Outputs[counter].Seldepth = Startoutput.Seldepth - 1;
                Outputs[counter].averageDepth = Startoutput.averageDepth - (int)Startoutput.Tree.Denominator;
            }
            else
                Outputs[counter].Tree = ChildNode;

            counter++;
        }

    }
}


