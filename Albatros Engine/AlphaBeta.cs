using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
class AlphaBeta
{
    Stopwatch sw = new Stopwatch();
    public Halfkp_avx2 halfkp;
    public MoveGen MoveGenerator = new MoveGen();
    public Classic_Eval eval = new Classic_Eval();   
    Random random = new Random(59675943);
    int Nodecount = 0, MaxDepth = 0;
    long[][,] PieceHashes = new long[27][,];
    long BlackToMove;

    public AlphaBeta(byte[,] board, int[] king_pos)
    {
        HashFunctionInit();
        halfkp = new Halfkp_avx2(board, king_pos);
    }
    public void HashFunctionInit()
    {
        //init the color
        BlackToMove = randomlong();
        PieceHashes[0] = new long[9, 9];
        //init the other pieces
        for (int i = 1; i < 27; i++)
        {
            PieceHashes[i] = new long[9, 9];
            for (int j = 0; j < 9; j++)
                for (int k = 0; k < 9; k++)
                    PieceHashes[i][j, k] = randomlong();
        }
        Console.WriteLine("Done!");
    }
    public long randomlong()
    {
        return ((long)random.Next() << 32) + (long)random.Next();
    }
    public long ZobristHash(byte[,] InputBoard, byte color)
    {
        long Output = (1 - color) * BlackToMove;

        for (int i = 0; i < 9; i++)
            for (int j = 0; j < 9; j++)
                Output ^= PieceHashes[InputBoard[i, j]][i, j];

        return Output;
    }
    public List<int[]> MVVLVA(byte[,] InputBoard, List<int[]> Moves)
    {
        float AttackerValue = 0;
        float VictimValue = 0;
        float currentPieceValue = 0;
        List<float> Values = new List<float>();
        List<int[]> SortedMoves = new List<int[]>();
        foreach(int[] Move in Moves)
        {
            for (int i = 0; i < 2; i++)
            {
                switch (InputBoard[Move[2 * i], Move[2 * i + 1]] - (InputBoard[Move[2 * i], Move[2 * i + 1]] >> 4) * 0b10000)
                {
                    case 0b00000001:
                        currentPieceValue = 1;
                        break;
                    case 0b00000011:
                        currentPieceValue = 1;
                        break;
                    case 0b00000100:
                        currentPieceValue = 3;
                        break;
                    case 0b00000101:
                        currentPieceValue = 3;
                        break;
                    case 0b00001000:
                        currentPieceValue = 9;
                        break;
                    case 0b00001001:
                        currentPieceValue = 5;
                        break;
                    case 0b00001010:
                        currentPieceValue = 5;
                        break;
                    default:
                        currentPieceValue = 0;
                        break;
                }
                if (i == 0)
                    AttackerValue = currentPieceValue;
                else
                    VictimValue = currentPieceValue;
            }
            //Calculate most valuable victim least valuable attacker value
            currentPieceValue = VictimValue - AttackerValue;
            if (Values.Count > 0)
            {
                for (int i = 0; i < Values.Count; i++)
                {
                    if(Values[i] <= currentPieceValue)
                    {
                        Values.Insert(i, currentPieceValue);
                        SortedMoves.Insert(i, Move);
                        break;
                    }
                    else
                    {
                        Values.Add(currentPieceValue);
                        SortedMoves.Add(Move);
                        break;
                    }
                }
            }
            else
            {
                Values.Add(currentPieceValue);
                SortedMoves.Add(Move);
            }
        }
        return SortedMoves;
    }
    public int[] iterative_deepening(byte[,] InputBoard, byte color, int depthPly, bool NNUE_avx2)
    {
        //initialize the variables
        int[] Output = new int[0], MoveUndo, kingsquares = new int[4], kingpositions = new int[2];
        List<int[]> Moves = MoveGenerator.ReturnPossibleMoves(InputBoard, color), CleanedMoves = new List<int[]>();
        float alpha = -2, currentScore = -2;
        byte othercolor = (byte)(1 - color);
        Accumulator currentacc = new Accumulator(256);
        //start the stopwatch
        sw.Start();
        //the the accumulator position to the starting position
        halfkp.set_acc_from_position(InputBoard, MoveGenerator.FindKings(InputBoard));
        //copy the accumulator for the current position
        Array.Copy(halfkp.acc.Acc[0], currentacc.Acc[0], currentacc.Acc[0].Length);
        Array.Copy(halfkp.acc.Acc[1], currentacc.Acc[1], currentacc.Acc[1].Length);
        //copy the kingpositions in both forms
        Array.Copy(halfkp.kingsquares, kingsquares, 4);
        Array.Copy(halfkp.kingpositions, kingpositions, 2);
        //get only the legal moves
        foreach (int[] Move in Moves)
            if (Move.Length != 5 || !MoveGenerator.CastlingCheck(InputBoard, Move))
                CleanedMoves.Add(Move);

        //sort the moves for most valuable victim vs least valuable attacker
        CleanedMoves = MVVLVA(InputBoard, CleanedMoves);

        //perform i searches, i being the depth 
        for (int i = 1; i <= depthPly; i++)
        {
            foreach(int[] move in CleanedMoves)
            {
                //play the move
                InputBoard = MoveGenerator.PlayMove(InputBoard, color, move);
                //play the move in the accumulator
                halfkp.update_acc_from_move(MoveGenerator.UnmakeMove, color);

                //copy the unmake move into move undo
                MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
                Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);

                //find if the current position is a terminal position
                //determining the mate value 2 => not a terminal position , 0 => draw , 1 => mate for white , -1 => mate for black
                int matingValue = MoveGenerator.Mate(InputBoard, othercolor);
                //checking if the position is not a terminal node
                if (matingValue != 2)
                {
                    //if the position is a terminal node the value for the node is set to the mating value from the perspective of the current color
                    currentScore = (color - 1) * matingValue;
                    //if the value is better than the currently best value return it because no value can be better or worse
                    if (alpha < currentScore)
                    {
                        //undo the current move
                        InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);
                        //copy the old accumulator back inthe real accumulator
                        Array.Copy(currentacc.Acc[1], halfkp.acc.Acc[1], currentacc.Acc[1].Length);
                        Array.Copy(currentacc.Acc[0], halfkp.acc.Acc[0], currentacc.Acc[0].Length);
                        //copy the kingpositions back into the neural network
                        Array.Copy(kingsquares, halfkp.kingsquares, 4);
                        Array.Copy(kingpositions, halfkp.kingpositions, 2);
                        Output = move;
                        alpha = currentScore;
                        return Output;
                    }
                }
                else
                {
                    //if the current depth is 1 perform a quiescent search
                    if (i == 1)
                    {
                        currentScore = -quiescent_search(InputBoard, -20, -alpha, othercolor, NNUE_avx2, 0);
                        Nodecount++;
                    }
                    //else call the negamax function at the current depth minus 1
                    else
                        currentScore = -negamax(InputBoard, othercolor, i - 1, -20, -alpha, NNUE_avx2);

                    //determine if the current move is better than the currently best move
                    if (alpha <= currentScore)
                    {
                        Output = move;
                        alpha = currentScore;
                    }
                }
                //undo the current move
                InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);
                //copy the old accumulator back inthe real accumulator
                Array.Copy(currentacc.Acc[1], halfkp.acc.Acc[1], currentacc.Acc[1].Length);
                Array.Copy(currentacc.Acc[0], halfkp.acc.Acc[0], currentacc.Acc[0].Length);
                //copy the kingpositions back into the neural network
                Array.Copy(kingsquares, halfkp.kingsquares, 4);
                Array.Copy(kingpositions, halfkp.kingpositions, 2);
            }
            //after a finished search return the main informations 
            Console.WriteLine("info depth {2} seldepth {3} nodes {1} nps {4} score cp {0}", Math.Round(alpha * 100), Nodecount, i, i + MaxDepth, (int)((Nodecount * 1000) / ((float)sw.ElapsedMilliseconds + 0.001)));
            //stop and restart the stopwatch
            sw.Stop();
            sw.Restart();
            alpha = -2;
            currentScore = -2;
            Nodecount = 0;
            MaxDepth = 0;
        }
        //return the best move
        return Output;
    }
    public float negamax(byte[,] InputBoard, byte color, int depthPly, float alpha, float beta, bool NNUE_avx2)
    {
        //define the variables
        bool found_legal_position = false;
        float current_score = 0;
        byte othercolor = (byte)(1 - color);
        int[] MoveUndo, kingsquares = new int[4], kingpositions = new int[2], BestMove = new int[0];
        List<int[]> Moves = MoveGenerator.ReturnPossibleMoves(InputBoard, color), CleanedMoves = new List<int[]>();
        Accumulator currentacc = new Accumulator(256);

        //the the accumulator position to the starting position
        halfkp.set_acc_from_position(InputBoard, MoveGenerator.FindKings(InputBoard));
        //copy the accumulator for the current position
        Array.Copy(halfkp.acc.Acc[0], currentacc.Acc[0], currentacc.Acc[0].Length);
        Array.Copy(halfkp.acc.Acc[1], currentacc.Acc[1], currentacc.Acc[1].Length);
        //copy the kingpositions in both forms
        Array.Copy(halfkp.kingsquares, kingsquares, 4);
        Array.Copy(halfkp.kingpositions, kingpositions, 2);
        //if the position is legal
        if (Moves != null)
        {
            //get only the legal moves
            foreach (int[] Move in Moves)
                if (Move.Length != 5 || !MoveGenerator.CastlingCheck(InputBoard, Move))
                    CleanedMoves.Add(Move);

            //sort the moves for most valuable victim vs least valuable attacker
            CleanedMoves = MVVLVA(InputBoard, CleanedMoves);
        }
        //the position is illegal
        else
            return -2;

        foreach (int[] Move in CleanedMoves)
        {
            //play the current move on the board
            InputBoard = MoveGenerator.PlayMove(InputBoard, color, Move);
            MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
            Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);
            //play the move in the accumulator
            halfkp.update_acc_from_move(MoveGenerator.UnmakeMove, color);
            //if the current depth is 1 do a quiescent search
            if (depthPly <= 1)
            {
                current_score = -quiescent_search(InputBoard, -beta, -alpha, othercolor, NNUE_avx2, 0);
                Nodecount++;
            }
            //else just call the function recursively
            else
                current_score = -negamax(InputBoard, othercolor, depthPly - 1, -beta, -alpha, NNUE_avx2);

            //if the current score is not 2 the position is legal and therefore we have found a legal move
            if (current_score > alpha && current_score != 2)
            {
                found_legal_position = true;
                alpha = current_score;
                BestMove = Move;
            }

            //undo the current move
            InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);
            //copy the old accumulator back inthe real accumulator
            Array.Copy(currentacc.Acc[1], halfkp.acc.Acc[1], currentacc.Acc[1].Length);
            Array.Copy(currentacc.Acc[0], halfkp.acc.Acc[0], currentacc.Acc[0].Length);
            //copy the kingpositions back into the neural network
            Array.Copy(kingsquares, halfkp.kingsquares, 4);
            Array.Copy(kingpositions, halfkp.kingpositions, 2);

            //if the branch is not better then the currently best branch we can prune the other positions
            if (current_score >= beta)
            {
                return current_score;
            }
        }
        //if no move was legal return the score for mate
        if (!found_legal_position)
        {
            //mate
            if (MoveGenerator.CompleteCheck(InputBoard, othercolor))
                return -1;
            //stalemate
            else
                return 0;
        }
        else
        {
            //return the best score
            return alpha;
        }
    }
    public float quiescent_search(byte[,] InputBoard, float alpha, float beta, byte color, bool NNUE_avx2, int depthPly)
    {
        //define the variables
        float current_score = 0;
        byte othercolor = (byte)(1 - color);
        int[] MoveUndo, kingsquares = new int[4], kingpositions = new int[2], BestMove = new int[0];
        List<int[]> Moves = MoveGenerator.ReturnPossibleCaptures(InputBoard, color), CleanedMoves = new List<int[]>();
        Accumulator currentacc = new Accumulator(256);

        //the the accumulator position to the starting position
        halfkp.set_acc_from_position(InputBoard, MoveGenerator.FindKings(InputBoard));
        //copy the accumulator for the current position
        Array.Copy(halfkp.acc.Acc[0], currentacc.Acc[0], currentacc.Acc[0].Length);
        Array.Copy(halfkp.acc.Acc[1], currentacc.Acc[1], currentacc.Acc[1].Length);
        //copy the kingpositions in both forms
        Array.Copy(halfkp.kingsquares, kingsquares, 4);
        Array.Copy(halfkp.kingpositions, kingpositions, 2);
        //if the position is legal
        if (Moves != null)
        {
            //evaluate the position
            if (NNUE_avx2)
                current_score = halfkp.AccToOutput(halfkp.acc, color);
            else
                current_score = eval.PestoEval(InputBoard, othercolor);

            //if the position is quiet return the evaluation
            if (Moves.Count == 0)
            {
                MaxDepth = Math.Max(depthPly, MaxDepth);
                return current_score;
            }

            //get only the legal moves
            foreach (int[] Move in Moves)
                if (Move.Length != 5 || !MoveGenerator.CastlingCheck(InputBoard, Move))
                    CleanedMoves.Add(Move);

            //sort the moves for most valuable victim vs least valuable attacker
            CleanedMoves = MVVLVA(InputBoard, CleanedMoves);
        }
        //the position is illegal
        else
            return -2;

        foreach (int[] Move in CleanedMoves)
        {
            //play the current move on the board
            InputBoard = MoveGenerator.PlayMove(InputBoard, color, Move);
            MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
            Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);
            //play the move in the accumulator
            halfkp.update_acc_from_move(MoveGenerator.UnmakeMove, color);

            //calls itself recursively
            current_score = -quiescent_search(InputBoard, -beta, -alpha, othercolor, NNUE_avx2, depthPly + 1);

            //if the current score is not 2 the position is not illegal and therefore we have found a legal move
            if (current_score > alpha && current_score != 2)
            {
                alpha = current_score;
                BestMove = Move;
            }

            //undo the current move
            InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);
            //copy the old accumulator back inthe real accumulator
            Array.Copy(currentacc.Acc[1], halfkp.acc.Acc[1], currentacc.Acc[1].Length);
            Array.Copy(currentacc.Acc[0], halfkp.acc.Acc[0], currentacc.Acc[0].Length);
            //copy the kingpositions back into the neural network
            Array.Copy(kingsquares, halfkp.kingsquares, 4);
            Array.Copy(kingpositions, halfkp.kingpositions, 2);

            //if the branch is not better then the currently best branch we can prune the other positions
            if (current_score >= beta)
            {
                return current_score;
            }
        }

        //return the best score
        return alpha;       
    }
}
class HTableEntry
{
    public List<int[]> BestMoves = new List<int[]>();
    public float Score;
    public int depth;
    public bool NodeScoreIsExact = true;
    public HTableEntry(int[] Bestmove, float CurrentScore, int Currentdepth, bool ScoreIsExact)
    {
        BestMoves = new List<int[]>();
        BestMoves.Add(Bestmove);
        Score = CurrentScore;
        depth = Currentdepth;
        NodeScoreIsExact = ScoreIsExact;
    }
}
