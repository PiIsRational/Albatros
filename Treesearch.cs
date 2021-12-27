using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

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
    BranchThread MainBranch;
    public Random random;
    bool WrongPosition = false;
    bool stop = false;
    public bool wasStopped = false;
    public int[] UnmakeMove;
    public bool NetType = true;

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
        if (Count > 1)
        {
            ThreadTreesearches = new BranchThread[Count - 1];
            for (int i = 0; i < Count - 1; i++)
                ThreadTreesearches[i] = new BranchThread();
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
                ValueNet.BackPropagationHalfkp(Positions, 1);
                ValueNet.setHalfkpNet(new float[][][,] { ValueNet.HalfkpWeigthChanges }, new float[][][] { ValueNet.HalfkpBiasChange }, new float[][,] { ValueNet.HalfkpMatrixChange }, new float[][] { ValueNet.HalfkpMatrixBiasChange }, 0.5f);
                Console.WriteLine("The Error is {0}", ValueNet.CostOfHalfkpNet(Positions));
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
    public void SetNet(string File)
    {
        ValueNet.LoadNet(File , true);
    }
    public MCTSimOutput MonteCarloTreeSim(byte[,] InputBoard, byte color, int NodeCount, bool NewTree, bool Random, bool NNUE , bool Halfkp , bool HalfKav2)
    {
        MCTSimOutput Output = new MCTSimOutput();

        if (NewTree)
            CurrentTree = GetTree(NodeCount, false, NNUE, Halfkp, HalfKav2, new Node(InputBoard, color, null, 0) , false).Tree;
        else
            CurrentTree = GetTree(NodeCount, false, NNUE, Halfkp, HalfKav2, CurrentTree , false).Tree;
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

        Output.Position = PlayMove(CurrentTree.Board, CurrentTree.ChildNodes[BestscorePlace].Color, CurrentTree.ChildNodes[BestscorePlace].Move);
        Output.eval = CurrentTree.Numerator / CurrentTree.Denominator;
        CurrentTree = CurrentTree.ChildNodes.ToArray()[BestscorePlace];
        CurrentTree.Board = new byte[9, 9];
        Array.Copy(Output.Position, CurrentTree.Board, Output.Position.Length);
        return Output;
    }
    public int[]SortArray(int[] Input)
    {
        bool Sorted = false;
        int placea = 0;
        while(! Sorted)
        {
            Sorted = true;
            //Sorting Pass
            for(int i = 0; i < Input.Length - 1; i++)
            {
                if(Input[i] > Input[i+1])
                {
                    placea = Input[i];
                    Input[i] = Input[i + 1];
                    Input[i + 1] = placea;
                    Sorted = false;
                }
            }
        }
        return Input;
    }
    public int[][] MultithreadMcts(byte[,] InputBoard, byte color, int NodeCount, bool NNUE, bool Halfkp , bool HalfKav2 , int ThreadCount , bool Infinite , bool UseTime , long Time)
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
        MainBranch = new BranchThread();
        TreesearchOutput TreeGenOut = new TreesearchOutput();
        if (Mate(InputBoard, color) != 2)
        {
            int score = Mate(InputBoard, color);
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
            TreeGenOut = GetTree(1, false, NNUE, Halfkp, HalfKav2, new Node(InputBoard, color, null, -2) , false);

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
                        MainBranch.Trees[j].Board = PlayMove(MainBranch.Trees[j].Board, MainBranch.Trees[j].Color, MainBranch.Trees[j].Move);
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
                        ThreadTreesearches[i].Trees[j].Board = PlayMove(ThreadTreesearches[i].Trees[j].Board, ThreadTreesearches[i].Trees[j].Color, ThreadTreesearches[i].Trees[j].Move);
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
                    Tree.Numerator += -Tree.ChildNodes[i].Numerator;
                }
                //Output first Info
                Console.WriteLine("info depth {0} seldepth {1} time {4} nodes {5} score cp {2} nps {3}", (int)(AverageDepth / (Tree.Denominator + 0.000000001)) + 1, Seldepth + 1, Math.Round(CentipawnLoss(Tree.Numerator / Tree.Denominator)), (int)(((float)Tree.Denominator / (sw.ElapsedMilliseconds + 1)) * 1000), sw.ElapsedMilliseconds, Tree.Denominator);
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
            Score = Mate(currentBoard, currentColor);
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
        Score = Mate(currentBoard, currentColor);
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
        List<int[]> Moves = ReturnPossibleMoves(InputBoard, color);
        bool finished = false;
        byte otherColor = 0;
        if (color == 0)
            otherColor = 1;
        while (!finished)
        {
            int Place = random.Next(Moves.Count - 1);
            InputBoard = PlayMove(InputBoard, color, Moves[Place]);
            int[] MoveUnmake = UnmakeMove;

            if (!CompleteCheck(InputBoard, otherColor))
                return InputBoard;
            else
                InputBoard = UndoMove(InputBoard, MoveUnmake);
        }
        return InputBoard;
    }
    public byte[,] UndoMove(byte[,] Position , int[] MoveUndo)
    {
        if ((MoveUndo.Length / 3) * 3 == MoveUndo.Length)
        {
            for (int i = 0; i < MoveUndo.Length / 3; i++)
                    Position[MoveUndo[i * 3], MoveUndo[i * 3 + 1]] = (byte)MoveUndo[i * 3 + 2];
        }
        else
            Console.WriteLine("Error");
        return Position;
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
            InputBoard = PlayMove(InputBoard, color, Mooves[i]);
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
    public byte[,] PlayMove(byte[,] InputBoard, byte color, int[] Moove)
    {
        for (int x = 1; x < 9; x++)
            for (int y = 4; y < 6; y++)
                if (InputBoard[x, y] != 0 && InputBoard[x, y] >> 4 == color && InputBoard[x, y] - (InputBoard[x, y] >> 4) * 0b10000 == 0b10)
                    InputBoard[x, y]++;
        int k = Moove[0];
        int j = Moove[1];
        int[] CurrentMoove;
        if (Moove.Length == 4)
            CurrentMoove = new int[2] { Moove[2], Moove[3] };
        else if (Moove.Length == 5)
            CurrentMoove = new int[3] { Moove[2], Moove[3], Moove[4] };
        else
            CurrentMoove = new int[0];
        switch (InputBoard[k, j] - (InputBoard[k, j] >> 4) * 0b10000)
        {
            //PawnStart
            case 0b00000001:
                Array.Copy(PawnStartExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //NormalPawn
            case 0b00000011:
                Array.Copy(PawnDirectExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //Knight
            case 0b00000100:
                Array.Copy(NormalExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //Bishop
            case 0b00000101:
                Array.Copy(NormalExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //King Can Castle
            case 0b00000110:
                Array.Copy(KingCastleExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //Normal King
            case 0b00000111:
                Array.Copy(NormalExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //Queen
            case 0b00001000:
                Array.Copy(NormalExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //RookCanCastle
            case 0b00001001:
                Array.Copy(StartExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //Normal Rook
            case 0b00001010:
                Array.Copy(NormalExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
        }
        return InputBoard;
    }
    public TreesearchOutput GetTree(long NodeCount, bool Output, bool NNUE, bool Halfkp, bool HalfKav2, Node startNode, bool Infinite)
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
                    CurrentScore = node.GetScore(CurrentNode.Denominator);
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
                CurrentBoard = PlayMove(CurrentBoard, CurrentNode.Color, CurrentNode.Move);
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
                List<int[]> ChildNodeMoves = ReturnPossibleMoves(CurrentBoard, CurrentNode.Color);
                Array.Copy(CurrentBoard, BoardCopy, CurrentBoard.Length);
                float Predictor = 1;
                byte OtherColor = 0;
                bool End = true;
                if (CurrentNode.Color == 0)
                    OtherColor = 1;
                Counter = 0;
                foreach (int[] Move in ChildNodeMoves) 
                {
                    CurrentBoard = PlayMove(CurrentBoard, CurrentNode.Color, Move);
                    if (!CompleteCheck(CurrentBoard, OtherColor))
                    {
                        End = false;
                        Predictor = (eval.PestoEval(CurrentBoard, OtherColor) + 1) / 2;
                        CurrentNode.ChildNodes.Add(new Node(null, OtherColor, Move , Predictor));
                        if (CurrentNode.ChildNodes[Counter].GetScore(CurrentNode.Denominator) > BestScore)
                        {
                            BestScore = CurrentNode.ChildNodes[Counter].GetScore(CurrentNode.Denominator);
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

                    CurrentBoard = PlayMove(CurrentBoard, CurrentNode.Color, CurrentNode.ChildNodes.ToArray()[place].Move);
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
                    CurrentNode.MateValue = Mate(CurrentBoard, CurrentNode.Color);
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
    public int[] MinMaxAlphaBeta(byte[,] InputBoard, byte color, int depthPly , bool NNUE , bool Halfkp , bool HalfKav2)
    {
        int[] Output = new int[0];
        int[] MoveUndo;
        List<int[]> Moves = ReturnPossibleMoves(InputBoard, color);
        List<int[]> CleanedMoves = new List<int[]>();
        foreach (int[] Move in Moves)
        {
            if (Move.Length != 5 || !CastlingCheck(InputBoard, Move))
                CleanedMoves.Add(Move);
        }
        double BestScore = 0, CurrentScore = -2;
        byte Othercolor = 0;
        int counter = 0;

        if (color == 0)
            Othercolor = 1;

        if (depthPly <= 1)
        {
            foreach (int[] Move in CleanedMoves)
            {
                InputBoard = PlayMove(InputBoard, color, Move);
                MoveUndo = new int[UnmakeMove.Length];
                Array.Copy(UnmakeMove, MoveUndo, MoveUndo.Length);
                int MatingValue = Mate(InputBoard, Othercolor);
                if (!CompleteCheck(InputBoard, Othercolor))
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
                }
                if(MatingValue != 2)
                {
                    CurrentScore = MatingValue;
                    if (BestScore < CurrentScore)
                    {
                        Output = CleanedMoves.ToArray()[counter];
                        BestScore = CurrentScore;
                    }
                }
                InputBoard = UndoMove(InputBoard, MoveUndo);
                counter++;
            }
        }
        else
        {
            foreach (int[] Move in CleanedMoves) 
            {
                InputBoard = PlayMove(InputBoard, color, Move);
                MoveUndo = new int[UnmakeMove.Length];
                Array.Copy(UnmakeMove, MoveUndo, MoveUndo.Length);
                int MatingValue = Mate(InputBoard, Othercolor);
                if (!CompleteCheck(InputBoard, Othercolor))
                {
                    if (MatingValue == 2)
                    {
                        if (CurrentScore == -2)
                        {
                            CurrentScore = -MinMaxAlphaBetaScore(InputBoard, Othercolor, depthPly - 1, BestScore, false, NNUE , Halfkp , HalfKav2);
                            if (CurrentScore != -2)
                            {
                                Output = CleanedMoves.ToArray()[counter];
                                BestScore = CurrentScore;
                            }
                        }
                        else
                        {
                            CurrentScore = -MinMaxAlphaBetaScore(InputBoard, Othercolor, depthPly - 1, BestScore, true, NNUE , Halfkp , HalfKav2);
                            if (BestScore < CurrentScore)
                            {
                                Output = CleanedMoves.ToArray()[counter];
                                BestScore = CurrentScore;
                            }
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
                InputBoard = UndoMove(InputBoard, MoveUndo);
                counter++;
            }
        }
        Console.WriteLine("info cp : {0}", Math.Round(BestScore * 1500));
        return Output;
    }
    public double MinMaxAlphaBetaScore(byte[,] InputBoard, byte color, int depthPly, double LastBest, bool Activation , bool NNUE , bool Halfkp , bool HalfKav2)
    {
        List<int[]> Moves = ReturnPossibleMoves(InputBoard, color);
        double BestScore = -400, CurrentScore = 0;
        byte Othercolor = 0;
        int counter = 0;
        int[] MoveUndo;

        if (color == 0)
            Othercolor = 1;

        if (Moves != null)
        {
            if (depthPly <= 1)
            {
                foreach (int[] Move in Moves)
                {
                    if (Move.Length != 5 || !CastlingCheck(InputBoard, Move))
                    {
                        InputBoard = PlayMove(InputBoard, color, Move);
                        MoveUndo = new int[UnmakeMove.Length];
                        Array.Copy(UnmakeMove, MoveUndo, MoveUndo.Length);
                        if (!CompleteCheck(InputBoard, Othercolor))
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
                        }
                        InputBoard = UndoMove(InputBoard, MoveUndo);
                    }
                }
            }
            else
            {
                foreach (int[] Move in Moves)
                {
                    if (Move.Length != 5 || !CastlingCheck(InputBoard, Move))
                    {
                        InputBoard = PlayMove(InputBoard, color, Move);
                        MoveUndo = new int[UnmakeMove.Length];
                        Array.Copy(UnmakeMove, MoveUndo, MoveUndo.Length);
                        if (counter == 0)
                        {
                            CurrentScore = -MinMaxAlphaBetaScore(InputBoard, Othercolor, depthPly - 1, BestScore, false, NNUE , Halfkp , HalfKav2);
                            if (CurrentScore == 2)
                            {
                                InputBoard = UndoMove(InputBoard, MoveUndo);
                                return Mate(InputBoard, Othercolor);
                            }
                            else
                                InputBoard = UndoMove(InputBoard, MoveUndo);
                            if (CurrentScore != -2)
                            {
                                BestScore = CurrentScore;
                                counter++;
                            }
                        }
                        else
                        {
                            CurrentScore = -MinMaxAlphaBetaScore(InputBoard, Othercolor, depthPly - 1, BestScore, true, NNUE , Halfkp , HalfKav2);
                            if (CurrentScore == 2)
                            {
                                InputBoard = UndoMove(InputBoard, MoveUndo);
                                return Mate(InputBoard, Othercolor);
                            }
                            else
                                InputBoard = UndoMove(InputBoard, MoveUndo);
                            if (BestScore < CurrentScore)
                                BestScore = CurrentScore;
                        }
                        if (Activation && -BestScore < LastBest && counter != 0)
                            return BestScore;
                    }
                }
            }
        }
        else
        {
            return 2;
        }
        return BestScore;
    }
    public int PossiblePositionCounter(byte[,] board, int depthPly, byte color)
    {
        List<int[]> Moves = ReturnPossibleMoves(board, color);
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
                    if (Move.Length != 5 || !CastlingCheck(board, Move))
                    {
                        board = PlayMove(board, color, Move);
                        MoveUndo = new int[UnmakeMove.Length];
                        Array.Copy(UnmakeMove, MoveUndo, MoveUndo.Length);
                        if (CompleteCheck(board, newcolor))
                            OutputNumber--;
                        board = UndoMove(board, MoveUndo);
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
                    if (Move.Length != 5 || !CastlingCheck(board, Move))
                    {
                        board = PlayMove(board, color, Move);
                        int[] Moveunmake = new int[UnmakeMove.Length];
                        Array.Copy(UnmakeMove, Moveunmake, UnmakeMove.Length);
                        OutputNumber += PossiblePositionCounter(board, depthPly - 1, newcolor);
                        board = UndoMove(board, Moveunmake);
                    }
                }
            }
            return OutputNumber;
        }
    }
    public byte[,] PawnDirectExecuteMooves(byte[,] InputBoard, int[] Move, int X, int Y)
    {
        byte Copy = InputBoard[X, Y];

        //Queening
        if (Move.Length == 3)
        {
            switch (Move[2])
            {
                // EnPassent
                case 0:
                    UnmakeMove = new int[] { X, Y, Copy, Move[0], Move[1], 0, Move[0], Y, InputBoard[Move[0], Y] };
                    InputBoard[Move[0], Y] = 0;
                    break;
                case 1:
                    //Knight
                    Copy++;
                    UnmakeMove = new int[] { X, Y, Copy, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
                    break;
                case 2:
                    //Bishop
                    Copy += 2;
                    UnmakeMove = new int[] { X, Y, Copy, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
                    break;
                case 3:
                    //Queen
                    Copy += 5;
                    UnmakeMove = new int[] { X, Y, Copy, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
                    break;
                case 4:
                    //Rook
                    Copy += 7;
                    UnmakeMove = new int[] { X, Y, Copy, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
                    break;
            }
        }
        else
        {
            UnmakeMove = new int[] { X, Y, Copy, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
        }
        InputBoard[X, Y] = 0;
        InputBoard[Move[0], Move[1]] = Copy;

        return InputBoard;
    }
    public byte[,] KingCastleExecuteMooves(byte[,] InputBoard, int[] Move, int X, int Y)
    {
        byte Copy = (byte)(InputBoard[X, Y] + 1);

        if (Move.Length == 2)
        {
            UnmakeMove = new int[] { X, Y, Copy - 1, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
            InputBoard[X, Y] = 0;
            InputBoard[Move[0], Move[1]] = Copy;
        }
        // if Castelling
        else
        {
            byte KingColor = (byte)(InputBoard[X, Y] >> 4);
            byte EnemyColor = 0;
            if (KingColor == 0)
                EnemyColor = 1;
            //Copy the King to two new Squares
            InputBoard[(X + Move[0]) / 2, Y] = Copy;
            InputBoard[Move[0], Move[1]] = Copy;
            //Check if any of the Kings are in Check
            if (ReturnPossibleMoves(InputBoard, EnemyColor) != null)
            {
                //Old King Position = 0
                InputBoard[X, Y] = 0;
                //QueenSideCastle
                if (X - Move[0] == 2)
                {
                    UnmakeMove = new int[] { X, Y, Copy - 1, Move[0], Move[1], 0, X - 1, Y, 0, 1, Y, InputBoard[1, Y] };
                    InputBoard[X - 1, Y] = (byte)(InputBoard[1, Y] + 1);
                    InputBoard[1, Y] = 0;
                }
                //KingSideCastle
                else
                {
                    UnmakeMove = new int[] { X, Y, Copy - 1, Move[0], Move[1], 0, X + 1, Y, 0, 8, Y, InputBoard[8, Y] };
                    InputBoard[X + 1, Y] = (byte)(InputBoard[8, Y] + 1);
                    InputBoard[8, Y] = 0;
                }
            }
        }
        return InputBoard;
    }
    public bool CastlingCheck(byte[,] InputBoard, int[] Moove)
    {
        byte Copy = (byte)(InputBoard[Moove[0], Moove[1]] + 1);
        bool Output = false;
        byte[,] CurrentBoard = new byte[9, 9];

        Array.Copy(InputBoard, 0, CurrentBoard, 0, InputBoard.Length);

        if (Moove.Length == 5)
        {
            byte KingColor = (byte)(InputBoard[Moove[0], Moove[1]] >> 4);
            byte EnemyColor = 0;
            if (KingColor == 0)
                EnemyColor = 1;

            //Copy the King to two new Squares
            CurrentBoard[(Moove[0] + Moove[2]) / 2, Moove[1]] = Copy;
            CurrentBoard[Moove[2], Moove[3]] = Copy;

            //Check if any of the Kings are in Check
            if (CompleteCheck(CurrentBoard, EnemyColor))
                Output = true;
        }
        return Output;
    }
    public byte[,] StartExecuteMooves(byte[,] InputBoard, int[] Move, int X, int Y)
    {
        byte Copy = (byte)(InputBoard[X, Y] + 1);

        UnmakeMove = new int[] { X, Y, Copy - 1, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
        InputBoard[X, Y] = 0;
        InputBoard[Move[0], Move[1]] = Copy;

        return InputBoard;
    }
    public byte[,] PawnStartExecuteMooves(byte[,] InputBoard, int[] Move, int X, int Y)
    {
        byte Copy = (byte)(InputBoard[X, Y] + 2);
        InputBoard[X, Y] = 0;
        if (Move[1] == Y + 2 || Move[1] == Y - 2)
        {
            UnmakeMove = new int[] { X, Y, Copy - 2, Move[0], Move[1], 0 };
            InputBoard[Move[0], Move[1]] = (byte)(Copy - 1);
        }
        else
        {
            UnmakeMove = new int[] { X, Y, Copy - 2, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
            InputBoard[Move[0], Move[1]] = Copy;
        }

        return InputBoard;
    }
    public byte[,] NormalExecuteMooves(byte[,] InputBoard, int[] Move, int X, int Y)
    {
        byte Copy = InputBoard[X, Y];
        UnmakeMove = new int[] { X, Y, Copy, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
        InputBoard[X, Y] = 0;
        InputBoard[Move[0], Move[1]] = Copy;
        return InputBoard;
    }
    public List<int[]> ReturnPossibleMoves(byte[,] InputBoard, byte color)
    {
        List<int[]> Output = new List<int[]>();
        for (int i = 1; i < 9; i++)
            for (int j = 4; j < 6; j++)
                if (InputBoard[i, j] != 0 && InputBoard[i, j] >> 4 == color && InputBoard[i, j] - (InputBoard[i, j] >> 4) * 0b10000 == 0b10)
                    InputBoard[i, j]++;
        for (int i = 1; i < 9; i++)
        {
            for (int j = 1; j < 9; j++)
            {
                if (InputBoard[i, j] != 0 && InputBoard[i, j] >> 4 == color)
                {
                    switch (InputBoard[i, j] - (InputBoard[i, j] >> 4) * 0b10000)
                    {
                        case 0b00000001:
                            Output.AddRange(UpdatePieceOutput(PawnMoove(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00000011:
                            Output.AddRange(UpdatePawnOutput(PawnMoove(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00000100:
                            Output.AddRange(UpdatePieceOutput(KnightMoove(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00000101:
                            Output.AddRange(UpdatePieceOutput(BishopMoove(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00000110:
                            Output.AddRange(UpdatePieceOutput(KingMoove(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00000111:
                            Output.AddRange(UpdatePieceOutput(KingMoove(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00001000:
                            Output.AddRange(UpdatePieceOutput(QueenMoove(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00001001:
                            Output.AddRange(UpdatePieceOutput(RookMoove(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00001010:
                            Output.AddRange(UpdatePieceOutput(RookMoove(InputBoard, i, j, color), i, j));
                            break;

                    }
                    if (WrongPosition)
                    {
                        WrongPosition = false;
                        return null;
                    }
                }
            }
        }
        return Output;
    }
    public List<int[]> UpdatePieceOutput(List<int[]> InputList, int X, int Y)
    {
        List<int[]> OutputList = new List<int[]>();
        foreach (int[] Array in InputList)
        {
            if (Array.Length == 2)
                OutputList.Add(new int[4] { X, Y, Array[0], Array[1] });
            else
                OutputList.Add(new int[5] { X, Y, Array[0], Array[1], 0 });
        }
        return OutputList;
    }
    public List<int[]> UpdatePawnOutput(List<int[]> InputList, int X, int Y)
    {
        List<int[]> OutputList = new List<int[]>();
        foreach (int[] Array in InputList)
        {
            //if Promoting Pawn
            if (Array[1] == 8 || Array[1] == 1)
            {
                //Knight
                OutputList.Add(new int[5] { X, Y, Array[0], Array[1], 1 });
                //Bishop
                OutputList.Add(new int[5] { X, Y, Array[0], Array[1], 2 });
                //Queen 
                OutputList.Add(new int[5] { X, Y, Array[0], Array[1], 3 });
                //Rook
                OutputList.Add(new int[5] { X, Y, Array[0], Array[1], 4 });
            }
            else if(Array.Length == 3)
            {
                OutputList.Add(new int[5] { X, Y, Array[0], Array[1], 0 });
            }
            else
            {
                OutputList.Add(new int[4] { X, Y, Array[0], Array[1]});
            }
        }
        return OutputList;
    }
    public void check(byte[,] InputBoard, int X, int Y)
    {
        if ((InputBoard[X, Y] - (InputBoard[X, Y] >> 4) * 0b10000) >> 1 == 0b11)
        {
            WrongPosition = true;
        }
    }
    public bool CompleteCheck(byte[,] InputBoard, byte color)
    {
        for (int i = 1; i < 9; i++)
            for (int j = 4; j < 6; j++)
                if (InputBoard[i, j] != 0 && InputBoard[i, j] >> 4 == color && InputBoard[i, j] - (InputBoard[i, j] >> 4) * 0b10000 == 0b10)
                    InputBoard[i, j]++;
        for (int i = 1; i < 9; i++)
        {
            for (int j = 1; j < 9; j++)
            {
                if (InputBoard[i, j] != 0 && InputBoard[i, j] >> 4 == color)
                {
                    switch (InputBoard[i, j] - (InputBoard[i, j] >> 4) * 0b10000)
                    {
                        case 0b00000001:
                            PawnCheck(InputBoard, i, j, color);
                            break;
                        case 0b00000011:
                            PawnCheck(InputBoard, i, j, color);
                            break;
                        case 0b00000100:
                            KnightCheck(InputBoard, i, j, color);
                            break;
                        case 0b00000101:
                            BishopCheck(InputBoard, i, j, color);
                            break;
                        case 0b00000110:
                            KingCheck(InputBoard, i, j, color);
                            break;
                        case 0b00000111:
                            KingCheck(InputBoard, i, j, color);
                            break;
                        case 0b00001000:
                            QueenCheck(InputBoard, i, j, color);
                            break;
                        case 0b00001001:
                            RookCheck(InputBoard, i, j, color);
                            break;
                        case 0b00001010:
                            RookCheck(InputBoard, i, j, color);
                            break;
                    }
                    if (WrongPosition)
                    {
                        WrongPosition = false;
                        return true;
                    }
                }
            }
        }
        return false;
    }
    public List<int[]> KnightMoove(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        List<int[]> Output = new List<int[]>();

        if (PositionX + 2 <= 8)
        {
            if (PositionY - 1 >= 1 && InputBoard[PositionX + 2, PositionY - 1] == 0 || PositionY - 1 >= 1 && (InputBoard[PositionX + 2, PositionY - 1]) >> 4 != color)
            {
                check(InputBoard, PositionX + 2, PositionY - 1);
                Output.Add(new int[2] { PositionX + 2, PositionY - 1 });
            }
            if (PositionY + 1 <= 8 && InputBoard[PositionX + 2, PositionY + 1] == 0 || PositionY + 1 <= 8 && (InputBoard[PositionX + 2, PositionY + 1]) >> 4 != color)
            {
                check(InputBoard, PositionX + 2, PositionY + 1);
                Output.Add(new int[2] { PositionX + 2, PositionY + 1 });
            }

        }
        if (PositionX - 2 >= 1)
        {
            if (PositionY - 1 >= 1 && InputBoard[PositionX - 2, PositionY - 1] == 0 || PositionY - 1 >= 1 && (InputBoard[PositionX - 2, PositionY - 1]) >> 4 != color)
            {
                check(InputBoard, PositionX - 2, PositionY - 1);
                Output.Add(new int[2] { PositionX - 2, PositionY - 1 });
            }
            if (PositionY + 1 <= 8 && InputBoard[PositionX - 2, PositionY + 1] == 0 || PositionY + 1 <= 8 && (InputBoard[PositionX - 2, PositionY + 1]) >> 4 != color)
            {
                check(InputBoard, PositionX - 2, PositionY + 1);
                Output.Add(new int[2] { PositionX - 2, PositionY + 1 });
            }

        }
        if (PositionX + 1 <= 8)
        {
            if (PositionY - 2 >= 1 && InputBoard[PositionX + 1, PositionY - 2] == 0 || PositionY - 2 >= 1 && (InputBoard[PositionX + 1, PositionY - 2]) >> 4 != color)
            {
                check(InputBoard, PositionX + 1, PositionY - 2);
                Output.Add(new int[2] { PositionX + 1, PositionY - 2 });
            }


            if (PositionY + 2 <= 8 && InputBoard[PositionX + 1, PositionY + 2] == 0 || PositionY + 2 <= 8 && (InputBoard[PositionX + 1, PositionY + 2]) >> 4 != color)
            {
                check(InputBoard, PositionX + 1, PositionY + 2);
                Output.Add(new int[2] { PositionX + 1, PositionY + 2 });
            }
        }
        if (PositionX - 1 >= 1)
        {
            if (PositionY - 2 >= 1 && InputBoard[PositionX - 1, PositionY - 2] == 0 || PositionY - 2 >= 1 && (InputBoard[PositionX - 1, PositionY - 2]) >> 4 != color)
            {
                check(InputBoard, PositionX - 1, PositionY - 2);
                Output.Add(new int[2] { PositionX - 1, PositionY - 2 });
            }
            if (PositionY + 2 <= 8 && InputBoard[PositionX - 1, PositionY + 2] == 0 || PositionY + 2 <= 8 && (InputBoard[PositionX - 1, PositionY + 2]) >> 4 != color)
            {
                check(InputBoard, PositionX - 1, PositionY + 2);
                Output.Add(new int[2] { PositionX - 1, PositionY + 2 });
            }
        }
        return Output;
    }
    public List<int[]> BishopMoove(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        List<int[]> Output = new List<int[]>();

        for (int i = 1; i + PositionX <= 8 && i + PositionY <= 8; i++)
        {
            if (InputBoard[PositionX + i, PositionY + i] == 0)
                Output.Add(new int[2] { PositionX + i, PositionY + i });

            else
            {
                if (InputBoard[PositionX + i, PositionY + i] >> 4 != color)
                {
                    check(InputBoard, PositionX + i, PositionY + i);
                    Output.Add(new int[2] { PositionX + i, PositionY + i });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; PositionX - i >= 1 && PositionY - i >= 1; i++)
        {
            if (InputBoard[PositionX - i, PositionY - i] == 0)
                Output.Add(new int[2] { PositionX - i, PositionY - i });

            else
            {
                if (InputBoard[PositionX - i, PositionY - i] >> 4 != color)
                {
                    check(InputBoard, PositionX - i, PositionY - i);
                    Output.Add(new int[2] { PositionX - i, PositionY - i });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; PositionX - i >= 1 && i + PositionY <= 8; i++)
        {
            if (InputBoard[PositionX - i, PositionY + i] == 0)
                Output.Add(new int[2] { PositionX - i, PositionY + i });

            else
            {
                if (InputBoard[PositionX - i, PositionY + i] >> 4 != color)
                {
                    check(InputBoard, PositionX - i, PositionY + i);
                    Output.Add(new int[2] { PositionX - i, PositionY + i });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; i + PositionX <= 8 && PositionY - i >= 1; i++)
        {
            if (InputBoard[PositionX + i, PositionY - i] == 0)
                Output.Add(new int[2] { PositionX + i, PositionY - i });

            else
            {
                if (InputBoard[PositionX + i, PositionY - i] >> 4 != color)
                {
                    check(InputBoard, PositionX + i, PositionY - i);
                    Output.Add(new int[2] { PositionX + i, PositionY - i });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        return Output;
    }
    public List<int[]> RookMoove(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        List<int[]> Output = new List<int[]>();

        for (int i = PositionX + 1; i <= 8; i++)
        {
            if (InputBoard[i, PositionY] == 0)
                Output.Add(new int[2] { i, PositionY });

            else
            {
                if (InputBoard[i, PositionY] >> 4 != color)
                {
                    check(InputBoard, i, PositionY);
                    Output.Add(new int[2] { i, PositionY });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = PositionX - 1; i >= 1; i--)
        {
            if (InputBoard[i, PositionY] == 0)
                Output.Add(new int[2] { i, PositionY });

            else
            {
                if (InputBoard[i, PositionY] >> 4 != color)
                {
                    check(InputBoard, i, PositionY);
                    Output.Add(new int[2] { i, PositionY });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = PositionY + 1; i <= 8; i++)
        {
            if (InputBoard[PositionX, i] == 0)
                Output.Add(new int[2] { PositionX, i });

            else
            {
                if (InputBoard[PositionX, i] >> 4 != color)
                {
                    check(InputBoard, PositionX, i);
                    Output.Add(new int[2] { PositionX, i });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = PositionY - 1; i >= 1; i--)
        {
            if (InputBoard[PositionX, i] == 0)
                Output.Add(new int[2] { PositionX, i });

            else
            {
                if (InputBoard[PositionX, i] >> 4 != color)
                {
                    check(InputBoard, PositionX, i);
                    Output.Add(new int[2] { PositionX, i });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        return Output;

    }
    public List<int[]> QueenMoove(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        List<int[]> Output = new List<int[]>();

        for (int i = 1; i + PositionX <= 8 && i + PositionY <= 8; i++)
        {
            if (InputBoard[PositionX + i, PositionY + i] == 0)
                Output.Add(new int[2] { PositionX + i, PositionY + i });

            else
            {
                if (InputBoard[PositionX + i, PositionY + i] >> 4 != color)
                {
                    check(InputBoard, PositionX + i, PositionY + i);
                    Output.Add(new int[2] { PositionX + i, PositionY + i });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; PositionX - i >= 1 && PositionY - i >= 1; i++)
        {
            if (InputBoard[PositionX - i, PositionY - i] == 0)
                Output.Add(new int[2] { PositionX - i, PositionY - i });

            else
            {
                if (InputBoard[PositionX - i, PositionY - i] >> 4 != color)
                {
                    check(InputBoard, PositionX - i, PositionY - i);
                    Output.Add(new int[2] { PositionX - i, PositionY - i });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; PositionX - i >= 1 && i + PositionY <= 8; i++)
        {
            if (InputBoard[PositionX - i, PositionY + i] == 0)
                Output.Add(new int[2] { PositionX - i, PositionY + i });

            else
            {
                if (InputBoard[PositionX - i, PositionY + i] >> 4 != color)
                {
                    check(InputBoard, PositionX - i, PositionY + i);
                    Output.Add(new int[2] { PositionX - i, PositionY + i });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; i + PositionX <= 8 && PositionY - i >= 1; i++)
        {
            if (InputBoard[PositionX + i, PositionY - i] == 0)
                Output.Add(new int[2] { PositionX + i, PositionY - i });

            else
            {
                if (InputBoard[PositionX + i, PositionY - i] >> 4 != color)
                {
                    check(InputBoard, PositionX + i, PositionY - i);
                    Output.Add(new int[2] { PositionX + i, PositionY - i });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = PositionX + 1; i <= 8; i++)
        {
            if (InputBoard[i, PositionY] == 0)
                Output.Add(new int[2] { i, PositionY });

            else
            {
                if (InputBoard[i, PositionY] >> 4 != color)
                {
                    check(InputBoard, i, PositionY);
                    Output.Add(new int[2] { i, PositionY });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = PositionX - 1; i >= 1; i--)
        {
            if (InputBoard[i, PositionY] == 0)
                Output.Add(new int[2] { i, PositionY });

            else
            {
                if (InputBoard[i, PositionY] >> 4 != color)
                {
                    check(InputBoard, i, PositionY);
                    Output.Add(new int[2] { i, PositionY });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = PositionY + 1; i <= 8; i++)
        {
            if (InputBoard[PositionX, i] == 0)
                Output.Add(new int[2] { PositionX, i });

            else
            {
                if (InputBoard[PositionX, i] >> 4 != color)
                {
                    check(InputBoard, PositionX, i);
                    Output.Add(new int[2] { PositionX, i });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = PositionY - 1; i >= 1; i--)
        {
            if (InputBoard[PositionX, i] == 0)
                Output.Add(new int[2] { PositionX, i });

            else
            {
                if (InputBoard[PositionX, i] >> 4 != color)
                {
                    check(InputBoard, PositionX, i);
                    Output.Add(new int[2] { PositionX, i });
                    break;
                }
                else
                {
                    break;
                }
            }
        }
        return Output;
    }
    public List<int[]> KingMoove(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        List<int[]> Output = new List<int[]>();

        //Casteling
        if (InputBoard[PositionX, PositionY] - color * 0b10000 == 0b00000110 && InputBoard[PositionX + 1, PositionY] == 0 && InputBoard[PositionX + 2, PositionY] == 0 && InputBoard[PositionX + 3, PositionY] != 0 && InputBoard[PositionX + 3, PositionY] - color * 0b10000 == 0b00001001)
        {
            Output.Add(new int[3] { PositionX + 2, PositionY, 0 });
        }
        if (InputBoard[PositionX, PositionY] - color * 0b10000 == 0b00000110 && InputBoard[PositionX - 1, PositionY] == 0 && InputBoard[PositionX - 2, PositionY] == 0 && InputBoard[PositionX - 3, PositionY] == 0 && InputBoard[PositionX - 4, PositionY] != 0 && InputBoard[PositionX - 4, PositionY] - color * 0b10000 == 0b00001001)
        {
            Output.Add(new int[3] { PositionX - 2, PositionY, 0 });
        }

        // normal Mooves
        if (PositionX - 1 >= 1)
        {
            if (InputBoard[PositionX - 1, PositionY] == 0 || InputBoard[PositionX - 1, PositionY] >> 4 != color)
            {
                check(InputBoard, PositionX - 1, PositionY);
                Output.Add(new int[2] { PositionX - 1, PositionY });
            }
            if (PositionY + 1 <= 8 && InputBoard[PositionX - 1, PositionY + 1] == 0 || PositionY + 1 <= 8 && InputBoard[PositionX - 1, PositionY + 1] >> 4 != color)
            {
                check(InputBoard, PositionX - 1, PositionY + 1);
                Output.Add(new int[2] { PositionX - 1, PositionY + 1 });
            }
            if (PositionY - 1 >= 1 && InputBoard[PositionX - 1, PositionY - 1] == 0 || PositionY - 1 >= 1 && InputBoard[PositionX - 1, PositionY - 1] >> 4 != color)
            {
                check(InputBoard, PositionX - 1, PositionY - 1);
                Output.Add(new int[2] { PositionX - 1, PositionY - 1 });
            }
        }
        if (PositionX + 1 <= 8)
        {
            if (InputBoard[PositionX + 1, PositionY] == 0 || InputBoard[PositionX + 1, PositionY] >> 4 != color)
            {
                check(InputBoard, PositionX + 1, PositionY);
                Output.Add(new int[2] { PositionX + 1, PositionY });
            }
            if (PositionY + 1 <= 8 && InputBoard[PositionX + 1, PositionY + 1] == 0 || PositionY + 1 <= 8 && InputBoard[PositionX + 1, PositionY + 1] >> 4 != color)
            {
                check(InputBoard, PositionX + 1, PositionY + 1);
                Output.Add(new int[2] { PositionX + 1, PositionY + 1 });
            }
            if (PositionY - 1 >= 1 && InputBoard[PositionX + 1, PositionY - 1] == 0 || PositionY - 1 >= 1 && InputBoard[PositionX + 1, PositionY - 1] >> 4 != color)
            {
                check(InputBoard, PositionX + 1, PositionY - 1);
                Output.Add(new int[2] { PositionX + 1, PositionY - 1 });
            }
        }

        if (PositionY + 1 <= 8 && InputBoard[PositionX, PositionY + 1] == 0 || PositionY + 1 <= 8 && InputBoard[PositionX, PositionY + 1] >> 4 != color)
        {
            check(InputBoard, PositionX, PositionY + 1);
            Output.Add(new int[2] { PositionX, PositionY + 1 });
        }
        if (PositionY - 1 >= 1 && InputBoard[PositionX, PositionY - 1] == 0 || PositionY - 1 >= 1 && InputBoard[PositionX, PositionY - 1] >> 4 != color)
        {
            check(InputBoard, PositionX, PositionY - 1);
            Output.Add(new int[2] { PositionX, PositionY - 1 });
        }

        return Output;
    }
    public List<int[]> PawnMoove(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        List<int[]> Output = new List<int[]>();
        int colorminus = 1;

        if (color == 0)
            colorminus = -1;

        if (InputBoard[PositionX, PositionY] - color * 0b10000 == 0b1 && InputBoard[PositionX, PositionY + colorminus] == 0 && InputBoard[PositionX, PositionY + 2 * colorminus] == 0)
            Output.Add(new int[2] { PositionX, PositionY + 2 * colorminus });


        if (PositionY + colorminus >= 1 && PositionY + colorminus <= 8)
        {
            if (InputBoard[PositionX, PositionY + colorminus] == 0)
                Output.Add(new int[2] { PositionX, PositionY + 1 * colorminus });

            if (PositionX + 1 <= 8 && InputBoard[PositionX + 1, PositionY + colorminus] != 0 && InputBoard[PositionX + 1, PositionY + colorminus] >> 4 != color)
            {
                check(InputBoard, PositionX + 1, PositionY + 1 * colorminus);
                Output.Add(new int[2] { PositionX + 1, PositionY + 1 * colorminus });
            }
            if (PositionX - 1 >= 1 && InputBoard[PositionX - 1, PositionY + colorminus] != 0 && InputBoard[PositionX - 1, PositionY + colorminus] >> 4 != color)
            {
                check(InputBoard, PositionX - 1, PositionY + 1 * colorminus);
                Output.Add(new int[2] { PositionX - 1, PositionY + 1 * colorminus });
            }
        }
        //en passent
        if (PositionX + 1 <= 8 && PositionY == 4 + color && InputBoard[PositionX + 1, PositionY] != 0 && InputBoard[PositionX + 1, PositionY] >> 4 != color && InputBoard[PositionX + 1, PositionY] - (InputBoard[PositionX + 1, PositionY] >> 4) * 0b10000 == 0b00000010)
            Output.Add(new int[3] { PositionX + 1, PositionY + 1 * colorminus, 0 });

        if (PositionX - 1 >= 1 && PositionY == 4 + color && InputBoard[PositionX - 1, PositionY] != 0 && InputBoard[PositionX - 1, PositionY] >> 4 != color && InputBoard[PositionX - 1, PositionY] - (InputBoard[PositionX - 1, PositionY] >> 4) * 0b10000 == 0b00000010)
            Output.Add(new int[3] { PositionX - 1, PositionY + 1 * colorminus, 0 });
        return Output;
    }
    public void KnightCheck(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        if (PositionX + 2 <= 8)
        {
            if (PositionY - 1 >= 1 && InputBoard[PositionX + 2, PositionY - 1] != 0 && (InputBoard[PositionX + 2, PositionY - 1]) >> 4 != color)
                check(InputBoard, PositionX + 2, PositionY - 1);
            if (PositionY + 1 <= 8 && InputBoard[PositionX + 2, PositionY + 1] != 0 && (InputBoard[PositionX + 2, PositionY + 1]) >> 4 != color)
                check(InputBoard, PositionX + 2, PositionY + 1);
        }
        if (PositionX - 2 >= 1)
        {
            if (PositionY - 1 >= 1 && InputBoard[PositionX - 2, PositionY - 1] != 0 && (InputBoard[PositionX - 2, PositionY - 1]) >> 4 != color)
                check(InputBoard, PositionX - 2, PositionY - 1);
            if (PositionY + 1 <= 8 && InputBoard[PositionX - 2, PositionY + 1] != 0 && (InputBoard[PositionX - 2, PositionY + 1]) >> 4 != color)
                check(InputBoard, PositionX - 2, PositionY + 1);
        }
        if (PositionX + 1 <= 8)
        {
            if (PositionY - 2 >= 1 && InputBoard[PositionX + 1, PositionY - 2] != 0 && (InputBoard[PositionX + 1, PositionY - 2]) >> 4 != color)
                check(InputBoard, PositionX + 1, PositionY - 2);
            if (PositionY + 2 <= 8 && InputBoard[PositionX + 1, PositionY + 2] != 0 && (InputBoard[PositionX + 1, PositionY + 2]) >> 4 != color)
                check(InputBoard, PositionX + 1, PositionY + 2);
        }
        if (PositionX - 1 >= 1)
        {
            if (PositionY - 2 >= 1 && InputBoard[PositionX - 1, PositionY - 2] != 0 && (InputBoard[PositionX - 1, PositionY - 2]) >> 4 != color)
                check(InputBoard, PositionX - 1, PositionY - 2);
            if (PositionY + 2 <= 8 && InputBoard[PositionX - 1, PositionY + 2] != 0 && (InputBoard[PositionX - 1, PositionY + 2]) >> 4 != color)
                check(InputBoard, PositionX - 1, PositionY + 2);
        }
    }
    public void BishopCheck(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        for (int i = 1; i + PositionX <= 8 && i + PositionY <= 8; i++)
        {
            if (InputBoard[PositionX + i, PositionY + i] != 0)
            {
                if (InputBoard[PositionX + i, PositionY + i] >> 4 != color)
                {
                    check(InputBoard, PositionX + i, PositionY + i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; PositionX - i >= 1 && PositionY - i >= 1; i++)
        {
            if (InputBoard[PositionX - i, PositionY - i] != 0)
            {
                if (InputBoard[PositionX - i, PositionY - i] >> 4 != color)
                {
                    check(InputBoard, PositionX - i, PositionY - i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; PositionX - i >= 1 && i + PositionY <= 8; i++)
        {
            if (InputBoard[PositionX - i, PositionY + i] != 0)
            {
                if (InputBoard[PositionX - i, PositionY + i] >> 4 != color)
                {
                    check(InputBoard, PositionX - i, PositionY + i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; i + PositionX <= 8 && PositionY - i >= 1; i++)
        {
            if (InputBoard[PositionX + i, PositionY - i] != 0)
            {
                if (InputBoard[PositionX + i, PositionY - i] >> 4 != color)
                {
                    check(InputBoard, PositionX + i, PositionY - i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }
    }
    public void RookCheck(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        for (int i = PositionX + 1; i <= 8; i++)
        {
            if (InputBoard[i, PositionY] != 0)
            {
                if (InputBoard[i, PositionY] >> 4 != color)
                {
                    check(InputBoard, i, PositionY);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = PositionX - 1; i >= 1; i--)
        {
            if (InputBoard[i, PositionY] != 0)
            {
                if (InputBoard[i, PositionY] >> 4 != color)
                {
                    check(InputBoard, i, PositionY);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = PositionY + 1; i <= 8; i++)
        {
            if (InputBoard[PositionX, i] != 0)
            {
                if (InputBoard[PositionX, i] >> 4 != color)
                {
                    check(InputBoard, PositionX, i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = PositionY - 1; i >= 1; i--)
        {
            if (InputBoard[PositionX, i] != 0)
            {
                if (InputBoard[PositionX, i] >> 4 != color)
                {
                    check(InputBoard, PositionX, i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }
    }
    public void QueenCheck(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        for (int i = PositionX + 1; i <= 8; i++)
        {
            if (InputBoard[i, PositionY] != 0)
            {
                if (InputBoard[i, PositionY] >> 4 != color)
                {
                    check(InputBoard, i, PositionY);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = PositionX - 1; i >= 1; i--)
        {
            if (InputBoard[i, PositionY] != 0)
            {
                if (InputBoard[i, PositionY] >> 4 != color)
                {
                    check(InputBoard, i, PositionY);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = PositionY + 1; i <= 8; i++)
        {
            if (InputBoard[PositionX, i] != 0)
            {
                if (InputBoard[PositionX, i] >> 4 != color)
                {
                    check(InputBoard, PositionX, i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = PositionY - 1; i >= 1; i--)
        {
            if (InputBoard[PositionX, i] != 0)
            {
                if (InputBoard[PositionX, i] >> 4 != color)
                {
                    check(InputBoard, PositionX, i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; i + PositionX <= 8 && i + PositionY <= 8; i++)
        {
            if (InputBoard[PositionX + i, PositionY + i] != 0)
            {
                if (InputBoard[PositionX + i, PositionY + i] >> 4 != color)
                {
                    check(InputBoard, PositionX + i, PositionY + i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; PositionX - i >= 1 && PositionY - i >= 1; i++)
        {
            if (InputBoard[PositionX - i, PositionY - i] != 0)
            {
                if (InputBoard[PositionX - i, PositionY - i] >> 4 != color)
                {
                    check(InputBoard, PositionX - i, PositionY - i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; PositionX - i >= 1 && i + PositionY <= 8; i++)
        {
            if (InputBoard[PositionX - i, PositionY + i] != 0)
            {
                if (InputBoard[PositionX - i, PositionY + i] >> 4 != color)
                {
                    check(InputBoard, PositionX - i, PositionY + i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; i + PositionX <= 8 && PositionY - i >= 1; i++)
        {
            if (InputBoard[PositionX + i, PositionY - i] != 0)
            {
                if (InputBoard[PositionX + i, PositionY - i] >> 4 != color)
                {
                    check(InputBoard, PositionX + i, PositionY - i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }
    }
    public void KingCheck(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        // normal Mooves
        if (PositionX - 1 >= 1)
        {
            if (InputBoard[PositionX - 1, PositionY] != 0 && InputBoard[PositionX - 1, PositionY] >> 4 != color)
                check(InputBoard, PositionX - 1, PositionY);
            if (PositionY + 1 <= 8 && InputBoard[PositionX - 1, PositionY + 1] != 0 && InputBoard[PositionX - 1, PositionY + 1] >> 4 != color)
                check(InputBoard, PositionX - 1, PositionY + 1);
            if (PositionY - 1 >= 1 && InputBoard[PositionX - 1, PositionY - 1] != 0 && InputBoard[PositionX - 1, PositionY - 1] >> 4 != color)
                check(InputBoard, PositionX - 1, PositionY - 1);
        }
        if (PositionX + 1 <= 8)
        {
            if (InputBoard[PositionX + 1, PositionY] != 0 && InputBoard[PositionX + 1, PositionY] >> 4 != color)
                check(InputBoard, PositionX + 1, PositionY);
            if (PositionY + 1 <= 8 && InputBoard[PositionX + 1, PositionY + 1] != 0 && InputBoard[PositionX + 1, PositionY + 1] >> 4 != color)
                check(InputBoard, PositionX + 1, PositionY + 1);
            if (PositionY - 1 >= 1 && InputBoard[PositionX + 1, PositionY - 1] != 0 && InputBoard[PositionX + 1, PositionY - 1] >> 4 != color)
                check(InputBoard, PositionX + 1, PositionY - 1);
        }

        if (PositionY + 1 <= 8 && InputBoard[PositionX, PositionY + 1] != 0 && InputBoard[PositionX, PositionY + 1] >> 4 != color)
            check(InputBoard, PositionX, PositionY + 1);
        if (PositionY - 1 >= 1 && InputBoard[PositionX, PositionY - 1] != 0 && InputBoard[PositionX, PositionY - 1] >> 4 != color)
            check(InputBoard, PositionX, PositionY - 1);
    }
    public void PawnCheck(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        int colorminus = 1;
        if (color == 0)
            colorminus = -1;
        if (PositionY + colorminus >= 1 && PositionY + colorminus <= 8)
        {
            if (PositionX + 1 <= 8 && InputBoard[PositionX + 1, PositionY + colorminus] != 0 && InputBoard[PositionX + 1, PositionY + colorminus] >> 4 != color)
            {
                check(InputBoard, PositionX + 1, PositionY + 1 * colorminus);
            }
            if (PositionX - 1 >= 1 && InputBoard[PositionX - 1, PositionY + colorminus] != 0 && InputBoard[PositionX - 1, PositionY + colorminus] >> 4 != color)
            {
                check(InputBoard, PositionX - 1, PositionY + 1 * colorminus);
            }
        }
    }
    public int Mate(byte[,] InputBoard, byte Color)
    {
        //Look for blocking of Position
        List<int[]> Moves = ReturnPossibleMoves(InputBoard, Color);
        if (Moves == null)
            return -2;
        byte[,] MoveUndo = new byte[9,9];
        byte NewColor = 0;
        if (Color == 0)
            NewColor = 1;
        foreach (int[] Move in Moves)
        {
            if (Move.Length != 5 || !CastlingCheck(InputBoard, Move))
            {
                Array.Copy(InputBoard, MoveUndo, MoveUndo.Length);
                InputBoard = PlayMove(InputBoard, Color, Move);
                if (!CompleteCheck(InputBoard, NewColor))
                {
                    Array.Copy(MoveUndo, InputBoard, InputBoard.Length);
                    return 2;
                }
                else
                    Array.Copy(MoveUndo, InputBoard, InputBoard.Length);
            }
        }
        //Mate or Stalemate
        //Case: Mate
        if (CompleteCheck(InputBoard, NewColor))
            return (2 * NewColor - 1);
        //Case: Stalemate
        else
            return 0;
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
    public double GetScore(double FatherDenominator)
    {
        if (FatherDenominator == 1)
            return Probability;
        return (Numerator / (Denominator + 0.0000000001)) + 1.41 * Probability * (Math.Sqrt(FatherDenominator) / (Denominator + 0.00000000001));
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
        Startoutput = treesearch.GetTree(Nodecount, false, NNUE, HalfKp, HalfKav2, ReplacementRoot, Infinite);
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


