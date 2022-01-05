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
    public NNUE ValueNet = new NNUE();
    Hashtable hashtable = new Hashtable();
    
    Random random = new Random(59675943);
    int Nodecount = 0;
    int MaxDepth = 0;
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

    public int[] IterativeDeepening(byte[,] InputBoard, byte color, int depthPly, bool NNUE_avx2)
    {
        sw.Start();
        halfkp.set_acc_from_position(InputBoard, MoveGenerator.FindKings(InputBoard));
        //copy the accumulator for the current position
        Accumulator currentacc = new Accumulator(512);
        Array.Copy(halfkp.acc.Acc, currentacc.Acc, currentacc.Acc.Length);
        //if the current position is in the Hash Table
        long CurrentKey = ZobristHash(InputBoard, color);
        List<int[]> ProbablytheBestMoves = new List<int[]>();
        bool FoundEntry = false;
        HTableEntry entry = new HTableEntry(new int[0], 0, 0, false);
        if (hashtable.ContainsKey(CurrentKey))
        {
            FoundEntry = true;
            entry = (HTableEntry)hashtable[CurrentKey];
            if (depthPly <= entry.depth)
            {
                return entry.BestMoves[entry.BestMoves.Count - 1];
            }
            else
                ProbablytheBestMoves = entry.BestMoves;
        }
        int[] Output = new int[0];
        int[] MoveUndo;
        List<int[]> Moves = MoveGenerator.ReturnPossibleMoves(InputBoard, color);
        List<int[]> CleanedMoves = new List<int[]>();
        if (FoundEntry)
        {
            foreach (int[] Move in Moves)
                if (Move.Length != 5 || !MoveGenerator.CastlingCheck(InputBoard, Move))
                    CleanedMoves.Add(Move);

            CleanedMoves = MVVLVA(InputBoard, CleanedMoves);

            foreach (int[] Move in ProbablytheBestMoves)
                CleanedMoves.Insert(0, Move);
        }
        else
        {
            foreach (int[] Move in Moves)
                if (Move.Length != 5 || !MoveGenerator.CastlingCheck(InputBoard, Move))
                    CleanedMoves.Add(Move);

            CleanedMoves = MVVLVA(InputBoard, CleanedMoves);
        }
        float BestScore = -2, CurrentScore = -2;
        byte Othercolor = 0;
        int counter = 0;

        if (color == 0)
            Othercolor = 1;
        for (int i = 1; i <= depthPly; i++)
        {
            if (hashtable.ContainsKey(CurrentKey))
            {
                FoundEntry = true;
                entry = (HTableEntry)hashtable[CurrentKey];
                foreach (int[] Move in ProbablytheBestMoves)
                    CleanedMoves.Remove(Move);
                ProbablytheBestMoves = entry.BestMoves;
                foreach (int[] Move in ProbablytheBestMoves)
                    CleanedMoves.Insert(0, Move);
            }

            foreach (int[] Move in CleanedMoves)
            {
                //play the move
                InputBoard = MoveGenerator.PlayMove(InputBoard, color, Move);
                //play the move in the accumulator
                halfkp.update_acc_from_move(MoveGenerator.UnmakeMove, color);
                //ad the move to the nnue accumulator

                MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
                Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);
                if (!MoveGenerator.CompleteCheck(InputBoard, Othercolor))
                {
                    int MatingValue = MoveGenerator.Mate(InputBoard, Othercolor);

                    if (MatingValue != 2)
                    {
                        CurrentScore = MatingValue;
                        if (BestScore < CurrentScore)
                        {
                            Output = CleanedMoves.ToArray()[counter];
                            BestScore = CurrentScore;
                            return Output;
                        }
                    }

                    if (i == 1)
                    {
                        CurrentScore = -QuiescenceSearch(InputBoard, BestScore, Othercolor, NNUE_avx2, 0);
                        Nodecount++;
                    }
                    else
                        CurrentScore = -NegaMaxAlphaBetaScore(InputBoard, Othercolor, i - 1, BestScore, NNUE_avx2);

                    if (BestScore < CurrentScore)
                    {
                        Output = CleanedMoves.ToArray()[counter];
                        BestScore = CurrentScore;
                    }
                }
                InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);
                //copy the old acc back inthe real accumulator
                Array.Copy(currentacc.Acc, halfkp.acc.Acc, currentacc.Acc.Length);
                counter++;
            }
            Console.WriteLine("info depth {2} seldepth {3} nodes {1} nps {4} score cp {0}", Math.Round(BestScore * 100), Nodecount, i, i + MaxDepth, (int)(Nodecount / sw.ElapsedMilliseconds * 1000));
            if (!FoundEntry || entry.depth < i)
            {
                if (entry.depth < i)
                    hashtable.Remove(CurrentKey);
                if (entry.BestMoves[0].Length == 0)
                    entry = new HTableEntry(Output, BestScore, i, false);
                else if (Output.Length != 0)
                {
                    int[] ToRemove = new int[0];
                    foreach (int[] GoodMove in entry.BestMoves)
                        if (Array.Equals(GoodMove, Output))
                            ToRemove = GoodMove;
                    if (ToRemove.Length != 0)
                        entry.BestMoves.Remove(ToRemove);
                    entry.BestMoves.Add(Output);
                }
                hashtable.Add(CurrentKey, entry);
            }
            FoundEntry = false;
            BestScore = -2;
            MaxDepth = 0;
            CurrentScore = -2;
            Nodecount = 0;
            counter = 0;
        }
        sw.Stop();

        return Output;
    }
    public float NegaMaxAlphaBetaScore(byte[,] InputBoard, byte color, int depthPly, double LastBest, bool NNUE_avx2)
    {
        //copy the accumulator for the current position
        Accumulator currentacc = new Accumulator(512);
        Array.Copy(halfkp.acc.Acc, currentacc.Acc, currentacc.Acc.Length);
        //if the current position is in the Hash Table
        long CurrentKey = ZobristHash(InputBoard, color);
        List<int[]> ProbablyTheBestMoves = new List<int[]>();
        bool FoundEntry = false;
        HTableEntry entry = new HTableEntry(new int[0], 0, 0, false);
        if (hashtable.ContainsKey(CurrentKey))
        {
            FoundEntry = true;
            entry = (HTableEntry)hashtable[CurrentKey];
            if (depthPly == entry.depth)
            {
                return entry.Score;
            }
            else
            {
                if (entry.Score == -2)
                    return -2;
                ProbablyTheBestMoves = entry.BestMoves;
            }
        }
        List<int[]> Moves = MoveGenerator.ReturnPossibleMoves(InputBoard, color);
        List<int[]> CleanedMoves = new List<int[]>();
        int[] BestMove = new int[0];
        if (Moves != null)
        {
            if (FoundEntry)
            {
                foreach (int[] Move in Moves)
                    if (Move.Length != 5 || !MoveGenerator.CastlingCheck(InputBoard, Move))
                        CleanedMoves.Add(Move);

                CleanedMoves = MVVLVA(InputBoard, CleanedMoves);

                foreach (int[] Move in ProbablyTheBestMoves)
                    CleanedMoves.Insert(0, Move);
            }
            else
            {
                foreach (int[] Move in Moves)
                {
                    if (Move.Length != 5 || !MoveGenerator.CastlingCheck(InputBoard, Move))
                        CleanedMoves.Add(Move);
                }
                CleanedMoves = MVVLVA(InputBoard, CleanedMoves);
            }
        }
        else
        {
            return -2;
        }
        float BestScore = -2, CurrentScore = 0;
        byte Othercolor = 0;
        int[] MoveUndo;

        if (color == 0)
            Othercolor = 1;

        foreach (int[] Move in CleanedMoves)
        {
            //play the current move on the board
            InputBoard = MoveGenerator.PlayMove(InputBoard, color, Move);
            MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
            Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);
            if (!MoveGenerator.CompleteCheck(InputBoard, Othercolor))
            {
                //play the move in the accumulator
                halfkp.update_acc_from_move(MoveGenerator.UnmakeMove, color);
                if (depthPly <= 1)
                {
                    CurrentScore = -QuiescenceSearch(InputBoard, BestScore, Othercolor, NNUE_avx2, 0);
                    Nodecount++;
                }
                else
                    CurrentScore = -NegaMaxAlphaBetaScore(InputBoard, Othercolor, depthPly - 1, BestScore, NNUE_avx2);

                if (CurrentScore == 2)
                {
                    float Mate = MoveGenerator.Mate(InputBoard, Othercolor);
                    InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);
                    return Mate;
                }
                if (CurrentScore > BestScore)
                {
                    BestScore = CurrentScore;
                    BestMove = Move;
                }

                InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);

                if (-BestScore < LastBest)
                {
                    if (!FoundEntry || entry.depth < depthPly)
                    {
                        if (entry.depth < depthPly)
                            hashtable.Remove(CurrentKey);
                        if (entry.BestMoves[0].Length == 0)
                            entry = new HTableEntry(BestMove, BestScore, depthPly, false);
                        else
                        {
                            int[] ToRemove = new int[0];

                            foreach (int[] GoodMove in entry.BestMoves)
                                if (Array.Equals(GoodMove, BestMove))
                                    ToRemove = GoodMove;

                            if (ToRemove.Length != 0)
                                entry.BestMoves.Remove(ToRemove);

                            entry.BestMoves.Add(BestMove);
                        }
                        hashtable.Add(CurrentKey, entry);
                    }
                    return BestScore;
                }
                //copy the old acc back inthe real accumulator
                Array.Copy(currentacc.Acc, halfkp.acc.Acc, currentacc.Acc.Length);
            }
            else
                InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);
        }
        if (!FoundEntry || entry.depth < depthPly)
        {
            if (entry.depth < depthPly)
                hashtable.Remove(CurrentKey);
            if (entry.BestMoves[0].Length == 0)
                entry = new HTableEntry(BestMove, BestScore, depthPly, true);
            else if (BestMove.Length != 0)
            {
                int[] ToRemove = new int[0];

                foreach (int[] GoodMove in entry.BestMoves)
                    if (Array.Equals(GoodMove, BestMove))
                        ToRemove = GoodMove;

                if (ToRemove.Length != 0)
                    entry.BestMoves.Remove(ToRemove);

                entry.BestMoves.Add(BestMove);
            }
            hashtable.Add(CurrentKey, entry);
        }
        return BestScore;
    }
    public float QuiescenceSearch(byte[,] InputBoard, float LastBest, byte color, bool NNUE_avx2, int depthPly)
    {
        //copy the accumulator for the current position
        Accumulator currentacc = new Accumulator(512);
        Array.Copy(halfkp.acc.Acc, currentacc.Acc, currentacc.Acc.Length);
        List<int[]> Moves = MoveGenerator.ReturnPossibleCaptures(InputBoard, color);
        List<int[]> CleanedMoves = new List<int[]>();
        float BestScore = -2, CurrentScore = 0;
        byte Othercolor = 0;
        if (color == 0)
            Othercolor = 1;
        int[] MoveUndo;
        if (Moves != null)
        {
            if (!MoveGenerator.CompleteCheck(InputBoard, Othercolor))
            {
                if (NNUE_avx2)            
                    CurrentScore = halfkp.AccToOutput(halfkp.acc, color);
                else
                    CurrentScore = eval.PestoEval(InputBoard, Othercolor);
            }
            else
            {
                return 2;
            }
            if (-CurrentScore <= LastBest)
            {
                MaxDepth = Math.Max(depthPly, MaxDepth);
                return CurrentScore;
            }
            if (Moves.Count == 0)
            {
                MaxDepth = Math.Max(depthPly, MaxDepth);
                return CurrentScore;
            }
            foreach (int[] Move in Moves)
            {
                if (Move.Length != 5 || !MoveGenerator.CastlingCheck(InputBoard, Move))
                    CleanedMoves.Add(Move);
            }
            CleanedMoves = MVVLVA(InputBoard, CleanedMoves);
        }
        else
        {
            return -2;
        }

        foreach (int[] Move in CleanedMoves)
        {
            //play the current move on the board
            InputBoard = MoveGenerator.PlayMove(InputBoard, color, Move);
            //play the move in the accumulator
            halfkp.update_acc_from_move(MoveGenerator.UnmakeMove, color);
            MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
            Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);

            CurrentScore = -QuiescenceSearch(InputBoard, BestScore, Othercolor, NNUE_avx2, depthPly + 1);

            if (CurrentScore == 2)
            {
                float Mate = MoveGenerator.Mate(InputBoard, Othercolor);
                InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);
                return Mate;
            }

            BestScore = Math.Max(CurrentScore, BestScore);

            InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);

            if (-BestScore <= LastBest)
                return BestScore;
            //copy the old acc back inthe real accumulator
            Array.Copy(currentacc.Acc, halfkp.acc.Acc, currentacc.Acc.Length);
        }
        return BestScore;
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
                        currentPieceValue += 1;
                        break;
                    case 0b00000011:
                        currentPieceValue += 1;
                        break;
                    case 0b00000100:
                        currentPieceValue += 3;
                        break;
                    case 0b00000101:
                        currentPieceValue += 3;
                        break;
                    case 0b00001000:
                        currentPieceValue += 9;
                        break;
                    case 0b00001001:
                        currentPieceValue += 5;
                        break;
                    case 0b00001010:
                        currentPieceValue += 5;
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
