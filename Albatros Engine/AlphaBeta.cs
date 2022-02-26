using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.IO;
class AlphaBeta
{
    Stopwatch sw = new Stopwatch() , sw1 = new Stopwatch();
    bool stop = false;
    public NNUE_avx2 ValueNet;
    public MoveGen MoveGenerator = new MoveGen();
    public Classic_Eval eval = new Classic_Eval();   
    Random random = new Random(59675943);
    int Nodecount = 0, MaxDepth = 0 , capture_counter = 0;
    long[][,] PieceHashes = new long[27][,];
    byte[,] MVVLVA_array = new byte[27, 27];
    int[,][] killer_moves = new int[2, byte.MaxValue + 1][];
    float[,,] history_moves = new float[27, 9, 9];
    int[,,][] counter_Moves = new int[27, 9, 9][];
    long BlackToMove, time_to_use = 0;
    public byte[,] HashTable = new byte[0, 0];
    public AlphaBeta(int HashSize)
    {
        HashTable = new byte[HashSize * 55556 , 18];
        initMVVLVA();
        HashFunctionInit();
        ValueNet = new NNUE_avx2(true);
    }
    public void Stop()
    {
        stop = true;
    }
    public int[] TimedAlphaBeta(long Milliseconds, byte[,] InputBoard, byte color, bool NNUE_avx2, bool PrintInfo)
    {
        time_to_use = Milliseconds;
        Thread timer = new Thread(ThreadTimer);
        timer.Start();
        return iterative_deepening(InputBoard, color, byte.MaxValue, NNUE_avx2, PrintInfo);
    }
    public void ThreadTimer()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        while(stopwatch.ElapsedMilliseconds != time_to_use){ }
        stop = true;
    }
    public int[] iterative_deepening(byte[,] InputBoard, byte color, int depthPly, bool NNUE_avx2 , bool PrintInfo)
    {
        //initialize the variables
        List<int[]> last_best_moves = new List<int[]>();
        bool pv_node = true;
        int[] Output = new int[0], MoveUndo;
        List<int[]> Moves = MoveGenerator.ReturnPossibleMoves(InputBoard, color), CleanedMoves = new List<int[]>();
        pv_out current_variation = new pv_out(), pv = new pv_out();
        float alpha = -2, delta = 0, window_a = 0, window_b = 0, last_best = 0;
        byte othercolor = (byte)(1 - color);
        Accumulator currentacc = new Accumulator(128);
        //get the key for the position
        long key = ZobristHash(InputBoard, color);
        //start the stopwatch
        sw.Start();
        sw1.Start();
        //the the accumulator position to the starting position
        ValueNet.set_acc_from_position(InputBoard);
        //copy the accumulator for the current position
        Array.Copy(ValueNet.acc.Acc[0], currentacc.Acc[0], currentacc.Acc[0].Length);
        Array.Copy(ValueNet.acc.Acc[1], currentacc.Acc[1], currentacc.Acc[1].Length);
        //get only the legal moves
        foreach (int[] Move in Moves)
            if (Move.Length != 5 || !MoveGenerator.CastlingCheck(InputBoard, Move))
                CleanedMoves.Add(Move);

        //sort the moves for most valuable victim vs least valuable attacker history and killer heuristics
        CleanedMoves = sort_moves(InputBoard, CleanedMoves, (byte)depthPly);

        //check if the current position is already in the Hash Table
        if (IsvalidEntry(key) == 1)
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

        for (int current_depth = 1; current_depth <= depthPly; current_depth++)
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

            if (current_depth >= 4)
            {
                //if the current depth is larger then 4 readjust the window
                delta = 0.5f;
                window_a = Sigmoid(UndoSigmoid(last_best) - delta, 4.2f);
                window_b = Sigmoid(UndoSigmoid(last_best) + delta, 4.2f);
                alpha = window_a;
                pv_node = true;
            }
            else
            {
                window_a = -2;
                window_b = 2;
                alpha = window_a;
            }

            while (!stop)
            {
                foreach (int[] move in CleanedMoves)
                {
                    current_variation = new pv_out();
                    //play the move
                    InputBoard = MoveGenerator.PlayMove(InputBoard, color, move);
                    //play the move in the accumulator
                    ValueNet.update_acc_from_move(InputBoard, MoveGenerator.UnmakeMove);
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
                        current_variation.Value = (2 * color - 1) * matingValue;
                        //if the value is better than the currently best value return it because no value can be better or worse
                        if (alpha < current_variation.Value)
                        {
                            //undo the current move
                            InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);
                            //copy the old accumulator back inthe real accumulator
                            Array.Copy(currentacc.Acc[1], ValueNet.acc.Acc[1], currentacc.Acc[1].Length);
                            Array.Copy(currentacc.Acc[0], ValueNet.acc.Acc[0], currentacc.Acc[0].Length);
                            Output = move;
                            alpha = current_variation.Value;
                            pv = current_variation;
                            pv.principalVariation.Add(Output);
                            return Output;
                        }
                    }
                    else
                    {
                        if (!MoveGenerator.CompleteCheck(InputBoard, color))
                        {
                            //if the current depth is 1 perform a quiescent search
                            if (current_depth == 1)
                            {
                                current_variation.Value = -quiescent_search(InputBoard, -window_b, -alpha, othercolor, NNUE_avx2, 0);
                                current_variation.principalVariation.Add(move);
                            }
                            //else call the negamax function at the current depth minus 1
                            else
                            {
                                //perform a search
                                current_variation = primary_variation_search(InputBoard, othercolor, current_depth - 1, -window_b, -alpha, NNUE_avx2, pv_node);
                                current_variation.Value = -current_variation.Value;
                                current_variation.principalVariation.Insert(0, move);
                            }

                            //determine if the current move is better than the currently best move only if it is legal
                            if (alpha < current_variation.Value && current_variation.Value != 2)
                            {
                                pv_node = false;
                                alpha = current_variation.Value;
                                pv = current_variation;
                            }
                        }
                    }
                    //undo the current move
                    InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);
                    //copy the old accumulator back inthe real accumulator
                    Array.Copy(currentacc.Acc[1], ValueNet.acc.Acc[1], currentacc.Acc[1].Length);
                    Array.Copy(currentacc.Acc[0], ValueNet.acc.Acc[0], currentacc.Acc[0].Length);

                    if (stop)
                        break;
                }

                if (alpha <= window_a || alpha >= window_b)
                {
                    window_a = -2;
                    window_b = 2;
                    alpha = window_a;
                }
                else
                    break;
            }
            if (!stop)
                Output = pv.principalVariation[0];
            //add the best move to the hash table
            AddToTable(Output, current_depth, alpha, key);
            //after a finished search return the main informations 
            if (PrintInfo && !stop)
            {
                if (alpha != 1 && alpha != -1)
                    Console.WriteLine("info depth {2} seldepth {3} nodes {1} nps {4} time {5} score cp {0} pv {6}", Math.Round(UndoSigmoid(alpha) * 100), Nodecount, current_depth, current_depth + MaxDepth, (int)(((float)(Nodecount) / (float)sw.ElapsedMilliseconds + 0.01) * 1000), (int)(sw1.ElapsedMilliseconds), variation_to_string(pv.principalVariation));
                else
                {
                    Console.WriteLine("info depth {2} seldepth {3} nodes {1} nps {4} time {5} score mate {0} pv {6}", pv.principalVariation.Count / 2, Nodecount, current_depth, current_depth + MaxDepth, (int)(((float)(Nodecount) / (float)sw.ElapsedMilliseconds + 0.01) * 1000), (int)(sw1.ElapsedMilliseconds), variation_to_string(pv.principalVariation));
                    break;
                }
            }
            //stop and restart the stopwatch
            sw.Stop();
            sw.Restart();

            if (stop)
            {
                stop = false;
                break;
            }
            last_best = alpha;
            alpha = -2;
            Nodecount = 0;
            MaxDepth = 0;
        }
        //stop the stopwatch
        sw1.Stop();
        sw1.Reset();
        //return the best move
        return Output;
    }
    public pv_out primary_variation_search(byte[,] InputBoard, byte color, int depthPly, float alpha, float beta, bool NNUE_avx2, bool pv_node)
    {
        //define the variables
        byte othercolor = (byte)(1 - color);
        bool found_legal_position = false, search_pv = true, check = false;
        int[] MoveUndo, BestMove = new int[0];
        int movecount = 0;
        Accumulator currentacc = new Accumulator(128);
        pv_out Output = new pv_out(), current_variation = new pv_out();
        Output.Value = alpha;
        HTableEntry entry = new HTableEntry(new int[0], 0, 0);
        if (depthPly >= 3 || depthPly == 1)
        {
            //calculate if the king is in check
            check = MoveGenerator.CompleteCheck(InputBoard, othercolor);
        }
        if (depthPly == 1 && check)
            depthPly++;
        //get the key for the position
        long key = ZobristHash(InputBoard, color);
        int KeyValid = IsvalidEntry(key);

        if (KeyValid > -2)
        {
            entry = GetInfoFromEntry(key);
            if (KeyValid == 1)
            {
                //if the position has the right depth return the value of the position
                if (entry.depth == depthPly)
                {
                    Output.principalVariation.Add(entry.BestMove);
                    Output.Value = entry.Score;
                    return Output;
                }
            }
        }

        //NULL Move Pruning
        if (depthPly >= 3 && !check && !pv_node)
        {
            //Make null Move
            if (depthPly == 3)
                current_variation.Value = -quiescent_search(InputBoard, beta, 0.0001f - beta, othercolor, NNUE_avx2, 0);
            else
            {
                current_variation.Value = -zero_window_search(InputBoard, othercolor, depthPly - 1 - 2, 0.0001f - beta, NNUE_avx2);
                current_variation.Value = -current_variation.Value;
            }
            //Unmake the null move
            if (current_variation.Value >= beta)
            {
                Output.principalVariation = new List<int[]>();
                Output.Value = beta;
                return Output;
            }
        }

        List<int[]> Moves = MoveGenerator.ReturnPossibleMoves(InputBoard, color), CleanedMoves = new List<int[]>();
        //copy the accumulator for the current position
        Array.Copy(ValueNet.acc.Acc[0], currentacc.Acc[0], currentacc.Acc[0].Length);
        Array.Copy(ValueNet.acc.Acc[1], currentacc.Acc[1], currentacc.Acc[1].Length);
        //if the position is legal
        if (Moves != null)
        {
            //get only the legal moves
            foreach (int[] Move in Moves)
                if (Move.Length != 5 || !MoveGenerator.CastlingCheck(InputBoard, Move))
                    CleanedMoves.Add(Move);

            //sort the moves for most valuable victim vs least valuable attacker
            CleanedMoves = sort_moves(InputBoard, CleanedMoves, (byte)depthPly);
        }
        //the position is illegal
        else
        {
            Output.Value = -2;
            return Output;
        }

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
            movecount++;
            current_variation = new pv_out();
            //play the current move on the board
            InputBoard = MoveGenerator.PlayMove(InputBoard, color, Move);

            MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
            Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);

            //play the move in the accumulator
            ValueNet.update_acc_from_move(InputBoard, MoveGenerator.UnmakeMove);

            //if the current depth is 1 do a quiescent search
            if (depthPly <= 1)
            {
                current_variation.Value = -quiescent_search(InputBoard, -beta, -alpha, othercolor, NNUE_avx2, 0);
                current_variation.principalVariation.Add(Move);
            }
            //else just call the function recursively
            else
            {
                if (search_pv)
                {
                    current_variation = primary_variation_search(InputBoard, othercolor, depthPly - 1, -beta, -alpha, NNUE_avx2, pv_node);
                    current_variation.Value = -current_variation.Value;
                    current_variation.principalVariation.Insert(0, Move);
                }
                else
                {
                    //late move reduction
                    if (depthPly >= 3 && !check && movecount >= capture_counter)
                    {
                        int decrease = 0;
                        if (movecount - capture_counter > 6)
                            decrease = depthPly / 3;
                        else
                            decrease = 1;
                        current_variation.Value = -zero_window_search(InputBoard, othercolor, depthPly - 1 - decrease, -alpha, NNUE_avx2);

                        if (current_variation.Value > alpha)
                            current_variation.Value = -zero_window_search(InputBoard, othercolor, depthPly - 1, -alpha, NNUE_avx2);
                    }
                    else
                        current_variation.Value = -zero_window_search(InputBoard, othercolor, depthPly - 1, -alpha, NNUE_avx2);

                    if (beta > current_variation.Value && current_variation.Value > alpha)
                    {
                        current_variation = primary_variation_search(InputBoard, othercolor, depthPly - 1, -beta, -alpha, NNUE_avx2, false);
                        current_variation.Value = -current_variation.Value;
                        current_variation.principalVariation.Insert(0, Move);
                    }
                }
            }
            //undo the current move
            InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);

            //copy the old accumulator back inthe real accumulator
            Array.Copy(currentacc.Acc[1], ValueNet.acc.Acc[1], currentacc.Acc[1].Length);
            Array.Copy(currentacc.Acc[0], ValueNet.acc.Acc[0], currentacc.Acc[0].Length);

            //if the current score is not 2 the position is legal and therefore we have found a legal move
            if (current_variation.Value != 2)
            {
                pv_node = false;
                found_legal_position = true;
                if (current_variation.Value > alpha)
                {
                    alpha = current_variation.Value;
                    search_pv = false;
                    BestMove = new int[Move.Length];
                    Array.Copy(Move, BestMove, Move.Length);

                    Output = current_variation;
                }
            }
            //if the branch is not better then the currently best branch we can prune the other positions
            if (current_variation.Value >= beta && current_variation.Value != 2)
            {
                //store the killer move
                if (InputBoard[Move[2], Move[3]] == 0)
                {
                    if (killer_moves[0, depthPly] != null)
                    {
                        killer_moves[1, depthPly] = new int[killer_moves[0, depthPly].Length];
                        Array.Copy(killer_moves[0, depthPly], killer_moves[1, depthPly], killer_moves[0, depthPly].Length);
                    }
                    killer_moves[0, depthPly] = Move;

                    history_moves[InputBoard[Move[0], Move[1]], Move[2], Move[3]] = depthPly * depthPly * 0.0001f;

                    counter_Moves[InputBoard[Move[0], Move[1]], Move[2], Move[3]] = Move;
                }

                //add the best move to the hash table if the current depth is greater than the depth of the entry or thre is no entry in the hash table
                if (BestMove.Length != 0)
                {
                    AddToTable(BestMove, 0, alpha, key);
                }

                Output.principalVariation = new List<int[]>();
                Output.Value = beta;
                return Output;
            }
            if (stop)
                break;
        }
        //if no move was legal return the score for mate
        if (!found_legal_position)
        {
            //mate
            if (MoveGenerator.CompleteCheck(InputBoard, othercolor))
            {
                Output.Value = -1;
                Output.principalVariation = new List<int[]>();
                return Output;
            }
            //stalemate
            else
            {
                Output.Value = 0;
                Output.principalVariation = new List<int[]>();
                return Output;
            }
        }
        else
        {
            //add the best move to the hash table if the current depth is greater than the depth of the entry or thre is no entry in the hash table
            if (BestMove.Length != 0)
            {
                AddToTable(BestMove, depthPly, alpha, key);
            }

            //return the best score
            return Output;
        }
    }
    public float zero_window_search(byte[,] InputBoard, byte color, int depthPly, float beta, bool NNUE_avx2)
    {
        //define the variables
        float alpha = beta - 0.0001f;
        bool found_legal_position = false;
        float current_score = 0;
        byte othercolor = (byte)(1 - color);
        int[] MoveUndo, BestMove = new int[0];
        Accumulator currentacc = new Accumulator(128);

        List<int[]> Moves = MoveGenerator.ReturnPossibleMoves(InputBoard, color), CleanedMoves = new List<int[]>();
        //copy the accumulator for the current position
        Array.Copy(ValueNet.acc.Acc[0], currentacc.Acc[0], currentacc.Acc[0].Length);
        Array.Copy(ValueNet.acc.Acc[1], currentacc.Acc[1], currentacc.Acc[1].Length);
        //if the position is legal
        if (Moves != null)
        {
            //get only the legal moves
            foreach (int[] Move in Moves)
                if (Move.Length != 5 || !MoveGenerator.CastlingCheck(InputBoard, Move))
                    CleanedMoves.Add(Move);

            //sort the moves for most valuable victim vs least valuable attacker
            CleanedMoves = sort_moves(InputBoard, CleanedMoves, (byte)depthPly);
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
            ValueNet.update_acc_from_move(InputBoard,MoveGenerator.UnmakeMove);

            //if the current depth is 1 do a quiescent search
            if (depthPly <= 1)
                current_score = -quiescent_search(InputBoard, -beta, -alpha, othercolor, NNUE_avx2, 0);
            //else just call the function recursively
            else
                current_score = -zero_window_search(InputBoard, othercolor, depthPly - 1, -alpha, NNUE_avx2);


            if (current_score != 2)
                found_legal_position = true;

            //undo the current move
            InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);

            //copy the old accumulator back inthe real accumulator
            Array.Copy(currentacc.Acc[1], ValueNet.acc.Acc[1], currentacc.Acc[1].Length);
            Array.Copy(currentacc.Acc[0], ValueNet.acc.Acc[0], currentacc.Acc[0].Length);

            //if the branch is not better then the currently best branch we can prune the other positions
            if (current_score >= beta && current_score != 2)
            {
                return beta;
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
            //return alpha
            return alpha;
        }
    }
    public float quiescent_search(byte[,] InputBoard, float alpha, float beta, byte color, bool NNUE_avx2, int depthPly)
    {
        //define the variables
        Nodecount++;
        float current_score = 0;
        byte othercolor = (byte)(1 - color);
        int[] MoveUndo;
        List<int[]> Moves = MoveGenerator.ReturnPossibleCaptures(InputBoard, color), CleanedMoves = new List<int[]>();
        Accumulator currentacc = new Accumulator(128);

        //copy the accumulator for the current position
        Array.Copy(ValueNet.acc.Acc[0], currentacc.Acc[0], currentacc.Acc[0].Length);
        Array.Copy(ValueNet.acc.Acc[1], currentacc.Acc[1], currentacc.Acc[1].Length);
        //if the position is legal
        if (Moves != null)
        {
            if (NNUE_avx2)
                current_score = ValueNet.AccToOutput(ValueNet.acc, color);
            else
                current_score = eval.PestoEval(InputBoard, othercolor);

            //if the branch is not better then the currently best branch we can prune the other positions
            if (current_score >= beta)
            {
                return beta;
            }

            //delta pruning
            if(UndoSigmoid(current_score) < UndoSigmoid(alpha) - 9)
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
            CleanedMoves = sort_moves(InputBoard, CleanedMoves, (byte)depthPly);
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
            ValueNet.update_acc_from_move(InputBoard, MoveGenerator.UnmakeMove);

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
            Array.Copy(currentacc.Acc[1], ValueNet.acc.Acc[1], currentacc.Acc[1].Length);
            Array.Copy(currentacc.Acc[0], ValueNet.acc.Acc[0], currentacc.Acc[0].Length);

            //if the branch is not better then the currently best branch we can prune the other positions
            if (current_score >= beta && current_score != 2)
            {
                return beta;
            }
        }
        //return the best score
        return alpha;       
    }
    public alpha_beta_output selfplay_iterative_deepening(byte[,] InputBoard, byte color, int depthPly, bool NNUE_avx2)
    {
        //initialize the variables
        int[] MoveUndo;
        List<int[]> Moves = MoveGenerator.ReturnPossibleMoves(InputBoard, color), CleanedMoves = new List<int[]>();
        float alpha = -2, currentScore = -2;
        byte othercolor = (byte)(1 - color);
        Accumulator currentacc = new Accumulator(128);
        alpha_beta_output Output = new alpha_beta_output();
        //get the key for the position
        long key = ZobristHash(InputBoard, color);
        //start the stopwatch
        sw.Start();
        sw1.Start();
        //the the accumulator position to the starting position
        ValueNet.set_acc_from_position(InputBoard);
        //copy the accumulator for the current position
        Array.Copy(ValueNet.acc.Acc[0], currentacc.Acc[0], currentacc.Acc[0].Length);
        Array.Copy(ValueNet.acc.Acc[1], currentacc.Acc[1], currentacc.Acc[1].Length);

        //get only the legal moves
        foreach (int[] Move in Moves)
            if (Move.Length != 5 || !MoveGenerator.CastlingCheck(InputBoard, Move))
                CleanedMoves.Add(Move);

        //sort the moves for most valuable victim vs least valuable attacker
        CleanedMoves = sort_moves(InputBoard, CleanedMoves, (byte)depthPly);

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
                ValueNet.update_acc_from_move(InputBoard, MoveGenerator.UnmakeMove);
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
                        alpha = currentScore;   
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
                        currentScore = -primary_variation_search(InputBoard, othercolor, i - 1, -2, -alpha, NNUE_avx2 , false).Value;

                    //determine if the current move is better than the currently best move only if it is legal
                    if (alpha < currentScore && currentScore != 2)
                    {
                        alpha = currentScore;

                    }
                }
                if (depthPly == i && currentScore != 2) 
                {
                    Output.Scores.Add(currentScore);
                    Output.movelist.Add(move);
                }
                //undo the current move
                InputBoard = MoveGenerator.UndoMove(InputBoard, MoveUndo);

                //copy the old accumulator back inthe real accumulator
                Array.Copy(currentacc.Acc[1], ValueNet.acc.Acc[1], currentacc.Acc[1].Length);
                Array.Copy(currentacc.Acc[0], ValueNet.acc.Acc[0], currentacc.Acc[0].Length);
            }
            //stop and restart the stopwatch
            alpha = -2;
            currentScore = -2;
            Nodecount = 0;
            MaxDepth = 0;
        }
        //return the best move
        return Output;
    }
    public void AddToTable(int[] Move, int depth, float Value, long key)
    { 
        int index = (int)(key % HashTable.GetLength(0));
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
    public List<int[]> sort_moves(byte[,] InputBoard, List<int[]> Moves , byte depthPly)
    {
        float currentPieceValue = 0;
        List<float> Values = new List<float>();
        List<int[]> SortedMoves = new List<int[]>();
        capture_counter = 0;
        foreach (int[] Move in Moves)
        {
            //Calculate MVVLVA value
            if (Move.Length == 4)
                currentPieceValue = MVVLVA_array[InputBoard[Move[0], Move[1]], InputBoard[Move[2], Move[3]]];
            else
            {
                switch (Move[4])
                {
                    // en passent
                    case 0:
                        currentPieceValue = MVVLVA_array[1, 1];
                        break;
                    // Knight promotion
                    case 1:
                        currentPieceValue = MVVLVA_array[1, 4];
                        break;
                    // Bishop promotion
                    case 2:
                        currentPieceValue = MVVLVA_array[1, 5];
                        break;
                    // Queen promotion
                    case 3:
                        currentPieceValue = MVVLVA_array[1, 8];
                        break;
                    // Rook promotion
                    case 5:
                        currentPieceValue = MVVLVA_array[1, 10];
                        break;
                }
            }

            //if the move is quiet
            if (currentPieceValue == 0)
            {
                if (IsEqual(killer_moves[0, depthPly], Move))
                    currentPieceValue = 300;
                else if (IsEqual(killer_moves[1, depthPly], Move))
                    currentPieceValue = 200;
                else if (IsEqual(counter_Moves[InputBoard[Move[0], Move[1]], Move[2], Move[3]], Move))
                    currentPieceValue = 100;

                currentPieceValue += history_moves[InputBoard[Move[0], Move[1]], Move[2], Move[3]];
            }
            else
            {
                capture_counter++;
                currentPieceValue += 400;
            }
            if (Values.Count > 0)
            {
                if (currentPieceValue == 0)
                {
                    Values.Add(currentPieceValue);
                    SortedMoves.Add(Move);
                }
                else
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
            }
            else
            {
                Values.Add(currentPieceValue);
                SortedMoves.Add(Move);
            }
        }
        return SortedMoves;
    }
    public void initMVVLVA()
    {
        int attacker = 0, victim = 0;
        byte attackervalue = 0, victimvalue = 0;
        for (byte i = 0; i < 27; i++)
        {
            attacker = i;
            //attacker
            switch (i)
            {
                case 0b00000001:
                    attackervalue = 5;
                    break;
                case 0b00000010:
                    attackervalue = 5;
                    break;
                case 0b00000011:
                    attackervalue = 5;
                    break;
                case 0b00000100:
                    attackervalue = 4;
                    break;
                case 0b00000101:
                    attackervalue = 3;
                    break;
                case 0b00001000:
                    attackervalue = 1;
                    break;
                case 0b00001001:
                    attackervalue = 2;
                    break;
                case 0b00001010:
                    attackervalue = 2;
                    break;

                case 0b00010001:
                    attackervalue = 5;
                    break;
                case 0b00010010:
                    attackervalue = 5;
                    break;
                case 0b00010011:
                    attackervalue = 5;
                    break;
                case 0b00010100:
                    attackervalue = 4;
                    break;
                case 0b00010101:
                    attackervalue = 3;
                    break;
                case 0b00011000:
                    attackervalue = 1;
                    break;
                case 0b00011001:
                    attackervalue = 2;
                    break;
                case 0b00011010:
                    attackervalue = 2;
                    break;
                default:
                    attackervalue = 0;
                    break;
            }
            for (int j = 0; j < 27; j++)
            {
                victim = j;
                switch (j)
                {
                    case 0b00000001:
                        victimvalue = 10;
                        break;
                    case 0b00000010:
                        victimvalue = 10;
                        break;
                    case 0b00000011:
                        victimvalue = 10;
                        break;
                    case 0b00000100:
                        victimvalue = 20;
                        break;
                    case 0b00000101:
                        victimvalue = 30;
                        break;
                    case 0b00001000:
                        victimvalue = 50;
                        break;
                    case 0b00001001:
                        victimvalue = 40;
                        break;
                    case 0b00001010:
                        victimvalue = 40;
                        break;

                    case 0b00010001:
                        victimvalue = 10;
                        break;
                    case 0b00010010:
                        victimvalue = 10;
                        break;
                    case 0b00010011:
                        victimvalue = 10;
                        break;
                    case 0b00010100:
                        victimvalue = 20;
                        break;
                    case 0b00010101:
                        victimvalue = 30;
                        break;
                    case 0b00011000:
                        victimvalue = 50;
                        break;
                    case 0b00011001:
                        victimvalue = 40;
                        break;
                    case 0b00011010:
                        victimvalue = 40;
                        break;
                    case 0b00000110:
                        victimvalue = 60;
                        break;
                    case 0b00000111:
                        victimvalue = 60;
                        break;
                    case 0b00010111:
                        victimvalue = 60;
                        break;
                    case 0b00010110:
                        victimvalue = 60;
                        break;
                    default:
                        victimvalue = 0;
                        break;
                }
                MVVLVA_array[attacker, victim] = (byte)(attackervalue + victimvalue);
            }
        }
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
        HTableEntry entry;
        for (int i = depth; i > 0; i--)
        {
            long key = ZobristHash(board, color);
            int KeyValid = IsvalidEntry(key);

            if (KeyValid == 1)
            {
                //get the entry from the transposition table
                entry = GetInfoFromEntry(key);
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
    public float Sigmoid(float Input, float Size)
    {
        return (Input / Size) / (float)Math.Sqrt((Input / Size) * (Input / Size) + 1);
    }
    public bool IsEqual(int[] Arr1, int[] Arr2)
    {
        if (Arr1 == null || Arr2 == null)
            return false;
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
class alpha_beta_output
{
    public List<int[]> movelist = new List<int[]>();
    public List<float> Scores = new List<float>();
}
class pv_out
{
    public float Value = 0;
    public List<int[]> principalVariation = new List<int[]>();
}

