using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.IO;
class AlphaBeta
{
    Stopwatch sw = new Stopwatch() , sw1 = new Stopwatch();
    public Halfkp_avx2 halfkp;
    public MoveGen MoveGenerator = new MoveGen();
    public Classic_Eval eval = new Classic_Eval();   
    Random random = new Random(59675943);
    int Nodecount = 0, MaxDepth = 0;
    long[][,] PieceHashes = new long[27][,];
    long BlackToMove;
    public byte[,] HashTable = new byte[0, 0];

    public AlphaBeta(byte[,] board, int[] king_pos , int HashSize)
    {
        HashTable = new byte[HashSize * 55556 , 18];
        HashFunctionInit();
        halfkp = new Halfkp_avx2(board, king_pos);
    }
    public int[] iterative_deepening(byte[,] InputBoard, byte color, int depthPly, bool NNUE_avx2)
    {
        //initialize the variables
        int[] Output = new int[0], MoveUndo, kingsquares = new int[4], kingpositions = new int[2];
        List<int[]> Moves = MoveGenerator.ReturnPossibleMoves(InputBoard, color), CleanedMoves = new List<int[]>();
        float alpha = -2, currentScore = -2;
        byte othercolor = (byte)(1 - color);
        Accumulator currentacc = new Accumulator(256);
        //get the key for the position
        long key = ZobristHash(InputBoard, color);
        //start the stopwatch
        sw.Start();
        sw1.Start();
        //the the accumulator position to the starting position
        //halfkp.set_acc_from_position(InputBoard, MoveGenerator.FindKings(InputBoard));
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

        //check if the current position is already in the Hash Table
        if(IsvalidEntry(key) == 1)
        {
            HTableEntry entry = GetInfoFromEntry(key);
            //if the position has the right depth return the best move
            if (entry.depth >= depthPly)
                return entry.BestMove;
            //else order the last best move first
            else
            {
                //order the last best move first
                for (int j = 0; j < CleanedMoves.Count; j++)
                    if (IsEqual(CleanedMoves[j], entry.BestMove))
                        CleanedMoves.RemoveAt(j);

                CleanedMoves.Insert(0, entry.BestMove);
            }
        }

        //perform i searches, i being the depth 
        for (int i = 1; i <= depthPly; i++)
        {
            //check if the current position is already in the Hash Table
            if (IsvalidEntry(key) == 1)
            {
                HTableEntry entry = GetInfoFromEntry(key);

                //order the last best move first
                for (int j = 0; j < CleanedMoves.Count; j++)
                    if (IsEqual(CleanedMoves[j], entry.BestMove))
                        CleanedMoves.RemoveAt(j);

                CleanedMoves.Insert(0, entry.BestMove);

            }

            foreach (int[] move in CleanedMoves)
            {
                //play the move
                InputBoard = MoveGenerator.PlayMove(InputBoard, color, move);
                //play the move in the accumulator
                //halfkp.update_acc_from_move(InputBoard, MoveGenerator.UnmakeMove, color);
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
                        currentScore = -quiescent_search(InputBoard, -2, -alpha, othercolor, NNUE_avx2, 0);
                        Nodecount++;
                    }
                    //else call the negamax function at the current depth minus 1
                    else
                        currentScore = -negamax(InputBoard, othercolor, i - 1, -2, -alpha, NNUE_avx2);

                    //determine if the current move is better than the currently best move only if it is legal
                    if (alpha < currentScore && currentScore != 2) 
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
            //add the best move to the hash table
            AddToTable(Output, i, alpha, key);
            //after a finished search return the main informations 
            Console.WriteLine("info depth {2} seldepth {3} nodes {1} nps {4} time {5} score cp {0} pv {6}", Math.Round(alpha * 100), Nodecount, i, i + MaxDepth, (int)(((float)Nodecount / (float)sw.ElapsedMilliseconds + 0.001) * 1000), (int)(sw1.ElapsedMilliseconds), variation_to_string(get_pv_from_tt(InputBoard, color, i)));
            //stop and restart the stopwatch
            sw.Stop();
            sw.Restart();
            alpha = -2;
            currentScore = -2;
            Nodecount = 0;
            MaxDepth = 0;
        }
        //stop the stopwatch
        sw1.Stop();
        sw1.Reset();
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
        Accumulator currentacc = new Accumulator(256);
        HTableEntry entry = new HTableEntry(new int[0], 0, 0);
        //get the key for the position
        long key = ZobristHash(InputBoard, color);
        int KeyValid = IsvalidEntry(key);
        bool logInfo = true;

        if (KeyValid > -2)
        {
            entry = GetInfoFromEntry(key);
            if (KeyValid == 1)
            {
                //if the position has the right depth return the value of the position
                if (entry.depth == depthPly)
                    return entry.Score;
            }
        }

        List<int[]> Moves = MoveGenerator.ReturnPossibleMoves(InputBoard, color), CleanedMoves = new List<int[]>();
        //the the accumulator position to the starting position
        //halfkp.set_acc_from_position(InputBoard, MoveGenerator.FindKings(InputBoard));
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

        //check if the current position is already in the Hash Table
        if (KeyValid == 1 && !IsEqual(entry.BestMove, new int[4]))  
        {
            bool didpasstrought = false;
            //order the last best move first
            for (int j = 0; j < CleanedMoves.Count; j++)
                if (IsEqual(CleanedMoves[j], entry.BestMove))
                {
                    CleanedMoves.RemoveAt(j);
                    didpasstrought = true;
                }
            if (didpasstrought) 
                  CleanedMoves.Insert(0, entry.BestMove);
        }

        foreach (int[] Move in CleanedMoves)
        {
            //play the current move on the board
            InputBoard = MoveGenerator.PlayMove(InputBoard, color, Move);

            MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
            Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);

            //play the move in the accumulator
            //halfkp.update_acc_from_move(InputBoard,MoveGenerator.UnmakeMove, color);

            //if the current depth is 1 do a quiescent search
            if (depthPly <= 1)
            {
                current_score = -quiescent_search(InputBoard, -beta, -alpha, othercolor, NNUE_avx2, 0);
            }
            //else just call the function recursively
            else
                current_score = -negamax(InputBoard, othercolor, depthPly - 1, -beta, -alpha, NNUE_avx2);

            //if the current score is not 2 the position is legal and therefore we have found a legal move
            if (current_score != 2)
            {
                if (depthPly == 1)
                    Nodecount++;
                found_legal_position = true;
                if (current_score > alpha)
                {
                    alpha = current_score;
                    BestMove = new int[Move.Length];
                    Array.Copy(Move, BestMove, Move.Length);
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

            //if the branch is not better then the currently best branch we can prune the other positions
            if (current_score >= beta && current_score != 2)
            {
                //add the best move to the hash table if the current depth is greater than the depth of the entry or thre is no entry in the hash table
                if (logInfo && BestMove.Length != 0)
                {
                    AddToTable(BestMove, depthPly, alpha, key);
                }

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
            //add the best move to the hash table if the current depth is greater than the depth of the entry or thre is no entry in the hash table
            if (logInfo && BestMove.Length != 0)
            {
                AddToTable(BestMove, depthPly, alpha, key);
            }

            //return the best score
            return alpha;
        }
    }
    public float quiescent_search(byte[,] InputBoard, float alpha, float beta, byte color, bool NNUE_avx2, int depthPly)
    {
        //define the variables
        float current_score = 0;
        byte othercolor = (byte)(1 - color);
        int[] MoveUndo, kingsquares = new int[4], kingpositions = new int[2];
        List<int[]> Moves = MoveGenerator.ReturnPossibleCaptures(InputBoard, color), CleanedMoves = new List<int[]>();
        Accumulator currentacc = new Accumulator(256);

        //the the accumulator position to the starting position
        //halfkp.set_acc_from_position(InputBoard, MoveGenerator.FindKings(InputBoard));
        //copy the accumulator for the current position
        Array.Copy(halfkp.acc.Acc[0], currentacc.Acc[0], currentacc.Acc[0].Length);
        Array.Copy(halfkp.acc.Acc[1], currentacc.Acc[1], currentacc.Acc[1].Length);
        //copy the kingpositions in both forms
        Array.Copy(halfkp.kingsquares, kingsquares, 4);
        Array.Copy(halfkp.kingpositions, kingpositions, 2);
        //if the position is legal
        if (Moves != null)
        {
            if (NNUE_avx2)
                current_score = halfkp.AccToOutput(halfkp.acc, color);
            else
                current_score = eval.PestoEval(InputBoard, othercolor);

            //if the branch is not better then the currently best branch we can prune the other positions
            if (current_score >= beta)
            {
                return current_score;
            }

            //delta pruning
            if(UndoSigmoid(current_score) < UndoSigmoid(alpha) - 10)
            {
                return alpha;
            }

            //if the current score is not 2 the position is not illegal and therefore we have found a legal move
            if (current_score > alpha && current_score != 2)
            {
                alpha = current_score;
            }

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
            //halfkp.update_acc_from_move(InputBoard, MoveGenerator.UnmakeMove, color);

            //calls itself recursively
            current_score = -quiescent_search(InputBoard, -beta, -alpha, othercolor, NNUE_avx2, depthPly + 1);

            //if the current score is not 2 the position is not illegal and therefore we have found a legal move
            if (current_score > alpha && current_score != 2)
            {
                alpha = current_score;
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
            if (current_score >= beta && current_score != 2)
            {
                return current_score;
            }
        }
        //return the best score
        return alpha;       
    }
    public void AddToTable(int[] Move, int depth, float Value, long key)
    { 
        int index = (int)(key % HashTable.GetLength(0));
        /*List<int[]> l = new List<int[]>();
        l.Add(Move);
        string content = "";
        if (File.Exists("In.csv"))
        {
            StreamReader sr = new StreamReader("In.csv");
            content = sr.ReadToEnd();
            sr.Close();
        }
        if (Move.Length < 4)
            content += depth + " ; " + Value + " ; " + " ; " + key;
        else
            content += depth + " ; " + Value + " ; " + variation_to_string(l) + " ; " + key;
        StreamWriter sw = new StreamWriter("In.csv");
        sw.WriteLine(content);
        sw.Close();*/
        //standart logging pattern
        /*
         * depth
         * 
         * then value
         * 
         * then Move
         * 
         * the the  key
         */
        byte[] Log = new byte[10];
        //save the depth
        Log[0] = (byte)depth;
        //save the evaluation
        Log[1] = BitConverter.GetBytes(Value)[0];
        Log[2] = BitConverter.GetBytes(Value)[1];
        Log[3] = BitConverter.GetBytes(Value)[2];
        Log[4] = BitConverter.GetBytes(Value)[3];

        for (int i = 5; i < 5 + Move.Length; i++)
            Log[i] = (byte)(Move[i - 5] + 1);

        byte[] keyArray = BitConverter.GetBytes(key);

        for (int i = 0; i < 10; i++)
            HashTable[index, i] = 0;

        for (int i = 0; i < 10; i++)
            HashTable[index, i] = Log[i];

        for (int i = 10; i < keyArray.Length + 10; i++)
            HashTable[index, i] = keyArray[i - 10];
    }
    public void printHashTable()
    {
        string Output = "";
        for (int i = 0; i < HashTable.GetLength(0); i++)
        {
            if (IsvalidEntry(i) > -2)
            {
                List<int[]> m = new List<int[]>();
                byte[] values = new byte[8];
                for (int j = 0; j < 8; j++)
                    values[j] = HashTable[i, 8 + j];

                long key = BitConverter.ToInt64(values);
                HTableEntry entry = GetInfoFromEntry(i);
                m.Add(entry.BestMove);
                Output += entry.depth + " ; " + entry.Score + " ; " + variation_to_string(m) + " ; " + key + "\n";
            }
        }
        StreamWriter sw = new StreamWriter("Out.csv");
        sw.WriteLine(Output);
        sw.Close();
    }
    public int IsvalidEntry(long key)
    {
        int index = (int)(key % HashTable.GetLength(0));
        if (HashTable[index, 0] != 0)
        {
            byte[] values = new byte[8];
            for (int i = 0; i < 8; i++)
                values[i] = HashTable[index, 10 + i];

            long Otherkey = BitConverter.ToInt64(values);

            if (Otherkey == key)
                return 1;
            else
                return -1;
        }
        else
        {
            return -2;
        }
    }
    public HTableEntry GetInfoFromEntry(long key)
    {
        int index = (int)(key % HashTable.GetLength(0));
        byte depth = HashTable[index, 0];
        byte[] EvalParts = new byte[4];
        EvalParts[0] = HashTable[index, 1];
        EvalParts[1] = HashTable[index, 2];
        EvalParts[2] = HashTable[index, 3];
        EvalParts[3] = HashTable[index, 4];
        float eval = BitConverter.ToSingle(EvalParts);
        int Movesize = 5;

        if (HashTable[index, 9] == 0)
            Movesize--;

        int[] Move = new int[Movesize];

        for (int i = 5; i < 5 + Movesize; i++)
            if (HashTable[index, i] != 0)
                Move[i - 5] = HashTable[index, i] - 1;
            else
                Move[i - 5] = 0;

        return new HTableEntry(Move, eval, depth);
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

        for (int i = 1; i < 9; i++)
            for (int j = 1; j < 9; j++)
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
        foreach (int[] Move in Moves)
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
                    if (Values[i] <= currentPieceValue)
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
    public string variation_to_string(List<int[]> Variation)
    {
        string[] ConvertNumToLetter = new string[] { "0", "a", "b", "c", "d", "e", "f", "g", "h" };
        string[] Promotion = new string[] { "", "n", "b", "q", "r" };
        string Output = "";
        foreach (int[] Move in Variation)
        {
            //Promoting Pawn
            if (Move.Length == 5)
                Output += ConvertNumToLetter[Move[0]] + Move[1] + ConvertNumToLetter[Move[2]] + Move[3] + Promotion[Move[4]] + " ";

            //Normal Piece
            else
                Output += ConvertNumToLetter[Move[0]] + Move[1] + ConvertNumToLetter[Move[2]] + Move[3] + " ";
        }
        return Output;
    }
    public List<int[]> get_pv_from_tt(byte[,] InputBoard , byte color , int depth)
    {
        byte[,] board = new byte[9, 9];
        Array.Copy(InputBoard, board, board.Length);
        List<int[]> Output = new List<int[]>();
        for (int i = depth; i > 0; i--)
        {
            long key = ZobristHash(board, color);
            int KeyValid = IsvalidEntry(key);

            if (KeyValid == 1)
            {
                //get the entry from the transposition table
                HTableEntry entry = GetInfoFromEntry(key);
                if (entry.depth == i)
                {
                    Output.Add(entry.BestMove);
                    board = MoveGenerator.PlayMove(board, color, entry.BestMove);
                    color = (byte)(1 - color);
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }
        return Output;
    }
    public float UndoSigmoid(float Input)
    {
        return (float)Math.Sqrt(Input * Input / (1 - Input * Input)) * 4.2f;
    }
    public bool IsEqual(int[] Arr1, int[] Arr2)
    {
        if (Arr1.Length != Arr2.Length)
            return false;

        for (int i = 0; i < Arr1.Length; i++)
            if (Arr1[i] != Arr2[i])
                return false;

        return true;
    }
}
class HTableEntry
{
    public int[] BestMove;
    public float Score;
    public byte depth;
    public HTableEntry(int[] Bestmove, float CurrentScore, byte Currentdepth)
    {
        BestMove = Bestmove;
        Score = CurrentScore;
        depth = Currentdepth;
    }
}

