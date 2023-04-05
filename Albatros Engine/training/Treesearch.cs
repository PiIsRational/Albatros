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

    public standart_chess chess_stuff = new standart_chess();
    public Classic_Eval eval = new Classic_Eval();
    public Node CurrentTree;
    public NNUE_avx2 ValueNet;
    public AlphaBeta alphaBeta = new AlphaBeta(100);
   // public BranchThread[] ThreadTreesearches;
    public Semaphore StopSemaphore = new Semaphore(1, 1);
    public movegen MoveGenerator = new movegen();
   // BranchThread MainBranch;
    public Random random;
    bool stop = false;
    bool use_nnue = false;
    public bool wasStopped = false;

    public Treesearch(int seed, bool LoadNet ,int ThreadCount)
    {
        random = new Random(seed);
        ValueNet = new NNUE_avx2(LoadNet);
        use_nnue = LoadNet;
       // ChangeThreadCount(ThreadCount);
    }
    /*public void ChangeThreadCount(int Count)
    {
        if (Count > 0)
            MainBranch = new BranchThread(use_nnue , 1);
        
        if (Count > 1)
        {
            ThreadTreesearches = new BranchThread[Count - 1];
            for (int i = 0; i < Count - 1; i++)
            {
                ThreadTreesearches[i] = new BranchThread(use_nnue, 1);
            }

        }
        else
            ThreadTreesearches = null;
    }*/
    public void SetNet(string File)
    {
        ValueNet.LoadNetFile(File);
    }
    public MCTSimOutput MonteCarloTreeSim(Position board, int NodeCount, bool NewTree, bool WeightedRandom, bool Random, bool NNUE, float c_puct, bool alpha_beta, int depth, bool opening, int[] movelist, ReverseMove undo_move)
    {
        MCTSimOutput Output = new MCTSimOutput();
        alpha_beta_output ab_tree_out = new alpha_beta_output();

        if(opening)
        {
            movelist = MoveGenerator.legal_move_generator(board, MoveGenerator.check(board, false), undo_move, movelist);
            List<double> score_list = new List<double>();
            int best_score_place = 0;
            byte newcolor = (byte)(board.color ^ 1);

            for (int i = 0; i < MoveGenerator.move_idx; i++) 
            {
                board = MoveGenerator.make_move(board, movelist[i], true, undo_move);

                score_list.Add(chess_stuff.convert_millipawn_to_wdl(-eval.pesto_eval(board)));

                board = MoveGenerator.unmake_move(board, undo_move);
            }
            if (score_list.Count != 0)
            {
                best_score_place = RandomWeightedChooser(score_list, 0.1);
                Output.Position = alphaBeta.play_move(board, movelist[best_score_place], false, null);
                Output.eval = (float)score_list[best_score_place];
              
            }
            else
            {
                Output.eval = 1;
                Output.Position = board;
            }
        }
        else if (alpha_beta)
        {

            ab_tree_out = alphaBeta.selfplay_iterative_deepening(board, depth, NNUE);
            if (ab_tree_out.draw)
            {
                Output.draw = true;
                Output.Position = board;
                Output.eval = 0;
            }
            else
            {
                Output.is_quiet = ab_tree_out.is_quiet;
                if (ab_tree_out.movelist.Count != 0)
                    Output.Position = alphaBeta.play_move(board, ab_tree_out.movelist[0], false, null);
                else
                    Output.Position = board;
                Output.eval = ab_tree_out.Score;
            }
            
        }
        /*
        else
        {
            if (NewTree)
                CurrentTree = GetTree(NodeCount, false, NNUE, new Node(board, color, null, 0), false, c_puct).Tree;
            else
                CurrentTree = GetTree(NodeCount, false, NNUE, CurrentTree, false, c_puct).Tree;
            int BestscorePlace = 0;

            List<double> Values = new List<double>();

            foreach (Node node in CurrentTree.ChildNodes)
                Values.Add(node.Numerator / node.Denominator);

            if (Random)
                BestscorePlace = random.Next(0, Values.Count - 1);
            else if (WeightedRandom)
                BestscorePlace = RandomWeightedChooser(Values, 1);
            else
                BestscorePlace = BestNumber(Values);


            Output.Position = MoveGenerator.PlayMove(CurrentTree.Board, CurrentTree.ChildNodes[BestscorePlace].Color, CurrentTree.ChildNodes[BestscorePlace].Move);
            Output.eval = CurrentTree.Numerator / CurrentTree.Denominator;
            CurrentTree = CurrentTree.ChildNodes.ToArray()[BestscorePlace];
            CurrentTree.Board = new byte[9, 9];
            Array.Copy(Output.Position, CurrentTree.Board, Output.Position.Length);
        }*/
        //chess_stuff.display_board(Output.Position);
        return Output;
    }
    /*
    public int[][] MultithreadMcts(byte[,] InputBoard, byte color, int NodeCount, bool NNUE, int ThreadCount , bool Infinite , bool UseTime , long Time , float c_puct)
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
            TreeGenOut = GetTree(1, false, NNUE, new Node(InputBoard, color, null, -2) , false , c_puct);

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
            Console.WriteLine("info depth {0} seldepth {1} time {4} nodes {5} score cp {2} nps {3}", (int)(AverageDepth / (Tree.Denominator + 0.000000001)) + 1, Seldepth + 1, -chess_stuff.convert_wdl_to_millipawn(Tree.Numerator / Tree.Denominator) / 10, (int)(((float)Tree.Denominator / (sw.ElapsedMilliseconds + 1)) * 1000), sw.ElapsedMilliseconds, Tree.Denominator);
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
                currentBoard = RandomSim(currentBoard, currentColor).Position;
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
    */
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
    /*
    public MCTSimOutput RandomSim(byte[,] board, byte color)
    {
        List<int[]> moves = MoveGenerator.ReturnPossibleMoves(board, color), cleaned_moves = new List<int[]>();
        MCTSimOutput output = new MCTSimOutput();
        //get only the legal moves
        foreach (int[] move in moves)
            if (!chess_stuff.is_castelling(move, board) || !MoveGenerator.CastlingCheck(board, move))
                cleaned_moves.Add(move);

        byte otherColor = (byte)(1 - color);

        //normal case
        while (cleaned_moves.Count > 0) 
        {
            int Place = random.Next(cleaned_moves.Count - 1);
            board = MoveGenerator.PlayMove(board, color, cleaned_moves[Place]);
            int[] MoveUnmake = MoveGenerator.UnmakeMove;

            if (!MoveGenerator.CompleteCheck(board, otherColor))
            {
                output.Position = board;
                return output;
            }

            board = MoveGenerator.UndoMove(board, MoveUnmake);
            cleaned_moves.RemoveAt(Place);
        }

        //if no legal move look for mate
        output.eval = 1;
        output.Position = board;
        return output;
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
    */
    public int RandomWeightedChooser(List<double> Input, double temperature)
    {
        int counter = 0;
        double Denominator = 0;
        double LastValue = 0;

        foreach (double Value in Input)
        {
            Denominator += Math.Pow((Value + 1) / 2, 1 / temperature);
            if (Value == 1)
                return counter;

            counter++;
        }

        double[] ValueList = new double[counter];
        counter = 0;

        foreach (double Value in Input)
        {
            ValueList[counter] = Math.Pow((Value + 1) / 2, 1 / temperature) + LastValue;
            LastValue = ValueList[counter];
            counter++;
        }

        double RamdomDouble = random.NextDouble();

        for (int i = 0; i < ValueList.Length; i++)
            if (RamdomDouble < ValueList[i] / Denominator)
                return i;

        return 0;
    }
    /*
    public TreesearchOutput GetTree(long NodeCount, bool Output, bool NNUE, Node startNode, bool Infinite, float c_puct)
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
                        Predictor = (chess_stuff.convert_millipawn_to_wdl(eval.pesto_eval(CurrentBoard, OtherColor)) + 1) / 2;
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
                        ValueNet.set_acc_from_position(CurrentBoard);
                        CurrentNumerator = ValueNet.AccToOutput(ValueNet.acc, OtherColor);
                    }
                    else
                        CurrentNumerator = chess_stuff.convert_millipawn_to_wdl(eval.pesto_eval(CurrentBoard, OtherColor));

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
                    ValueNet.set_acc_from_position(CurrentBoard);
                    CurrentNumerator = ValueNet.AccToOutput(ValueNet.acc, CurrentNode.Color);
                }
                else
                    CurrentNumerator = chess_stuff.convert_millipawn_to_wdl(eval.pesto_eval(CurrentBoard, CurrentNode.Color));

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
                Console.WriteLine("info depth {0} seldepth {1} time {4} nodes {5} score cp {2} nps {3}", averageDepth / (i + 1), seldepth, -Math.Round((Tree.Numerator / Tree.Denominator)), (int)(((float)i / (sw.ElapsedMilliseconds + 1)) * 1000), sw.ElapsedMilliseconds, Tree.Denominator);
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
    */
    public void SetStop(bool Input)
    {
        StopSemaphore.WaitOne();
        stop = Input;
        /*if (MainBranch != null)
            MainBranch.Stop();*/
        StopSemaphore.Release();
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
        return (Numerator / (Denominator + 0.0001)) + c_puct * Probability * (Math.Sqrt(FatherDenominator) / (Denominator + 0.0001));
    }
}
class TreesearchOutput
{
    public Node Tree;
    public int Seldepth = 0, averageDepth = 0;
}
class MCTSimOutput
{
    public bool is_quiet = false;
    public bool draw = false;
    public Position Position;
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
/*
class BranchThread
{
    public Treesearch treesearch;
    public int[] BranchNumber;
    public float c_puct = 1;
    public int Nodecount = 0;
    public bool NNUE = false;
    public bool Infinite = false;
    public Node[] Trees;
    Node ReplacementRoot;
    public TreesearchOutput[] Outputs;
    TreesearchOutput Startoutput;
    public BranchThread(bool load_netfile , int random_seed)
    {
        treesearch = new Treesearch(random_seed, load_netfile, 0);
    }
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
        Startoutput = treesearch.GetTree(Nodecount, false, NNUE, ReplacementRoot, Infinite, c_puct);
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
*/

