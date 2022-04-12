using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.IO;
class AlphaBeta
{
    Stopwatch sw = new Stopwatch();
   // StreamWriter stream_writer;
    bool stop = false;
    int[] sorting_counter = new int[300];

    public long[] repetitions = new long[307];
    bool[] repetion_lookup = new bool[ushort.MaxValue];
    
    public NNUE_avx2 ValueNet;
    public MoveGen MoveGenerator = new MoveGen();
    public Classic_Eval eval = new Classic_Eval();   
    Random random = new Random(59675943);
    int Nodecount = 0, max_ply = 0 , capture_counter = 0 , move_counter = 0 , root_depth = 0;
    bool[] null_move_pruning = new bool[byte.MaxValue + 1];

    long[][,] PieceHashes = new long[27][,];
    byte[,] MVVLVA_array = new byte[27, 27];

    int[,][] killer_moves = new int[2, byte.MaxValue + 1][];
    float[][,,,] history_moves = new float[2][,,,];
    float[] node_values = new float[byte.MaxValue + 1];
    int[][,,,][] counter_moves = new int[2][,,,][];
    public long BlackToMove, time_to_use = 0;
    public byte[,] HashTable = new byte[0, 0];

    public AlphaBeta(int HashSize)
    {
        history_moves[0] = new float[9, 9, 9, 9];
        history_moves[1] = new float[9, 9, 9, 9];
        HashTable = new byte[HashSize * 55556 , 18];
        initMVVLVA();
        HashFunctionInit();
        ValueNet = new NNUE_avx2(true);
        /*if (HashSize == 18)
            stream_writer = new StreamWriter("log.log");*/
    }
    public void Stop()
    {
        stop = true;
    }
    public int[] TimedAlphaBeta(long Milliseconds, byte[,] InputBoard, byte color, bool NNUE_avx2)
    {
        time_to_use = Milliseconds;
        Thread timer = new Thread(ThreadTimer);
        timer.Start();
        return iterative_deepening(InputBoard, color, byte.MaxValue, NNUE_avx2);
    }
    public void ThreadTimer()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        while(stopwatch.ElapsedMilliseconds <= time_to_use){ }
        stop = true;
    }
    public void reset_movesort()
    {
        //reset history moves
        history_moves[0] = new float[9, 9, 9, 9];
        history_moves[1] = new float[9, 9, 9, 9];

        //reset counter moves
        counter_moves[0] = new int[9, 9, 9, 9][];
        counter_moves[1] = new int[9, 9, 9, 9][];

        //reset killer moves
        killer_moves = new int[2, byte.MaxValue + 1][];
    }
    public int[] iterative_deepening(byte[,] board, byte color, int depth, bool NNUE_avx2)
    {
        //initialize the variables
        reset_movesort();
        stop = false;
        int[] EnPassent = new int[2];
        Array.Copy(MoveGenerator.EnPassent, EnPassent, EnPassent.Length);
        List<int[]> last_best_moves = new List<int[]>();
        bool search_pv = true, in_check = MoveGenerator.CompleteCheck(board, (byte)(1 - color));
        int[] Output = new int[0], MoveUndo;
        int movecount = 0 , fifty_move_rule = MoveGenerator.fifty_move_rule , capture_count;
        List<int[]> moves = MoveGenerator.ReturnPossibleMoves(board, color), cleaned_moves = new List<int[]>();
        pv_out current_variation = new pv_out(), pv = new pv_out();
        float alpha = -2, delta_a = 0, delta_b = 0, window_a = 0, window_b = 0, last_best = 0, last_last_best = 0, current_score = 0;
        byte othercolor = (byte)(1 - color);
        bool check = MoveGenerator.CompleteCheck(board, othercolor);
        Accumulator currentacc = new Accumulator(128);

        //get the key for the position
        long key = ZobristHash(board, color);

        //start the stopwatch
        sw.Start();
        sw.Start();

        //the the accumulator position to the starting position
        ValueNet.set_acc_from_position(board);

        //copy the accumulator for the current position
        Array.Copy(ValueNet.acc.Acc[0], currentacc.Acc[0], currentacc.Acc[0].Length);
        Array.Copy(ValueNet.acc.Acc[1], currentacc.Acc[1], currentacc.Acc[1].Length);

        //get only the legal moves
        foreach (int[] move in moves)
            if (move.Length != 5 || !MoveGenerator.CastlingCheck(board, move))
                cleaned_moves.Add(move);

        //sort the moves for most valuable victim vs least valuable attacker history and killer heuristics
        cleaned_moves = sort_moves(board, cleaned_moves, (byte)depth , color);
        capture_count = capture_counter;
        //check if the current position is already in the Hash Table
        if (IsvalidEntry(key) == 1)
        {
            TTableEntry entry = GetInfoFromEntry(key);
            //if the position has the right depth return the best move
            if (entry.depth >= depth)
                return entry.BestMove;
            //else order the last best move first
            else
            {
                //order the last best move first
                for (int j = 0; j < cleaned_moves.Count; j++)
                    if (IsEqual(cleaned_moves[j], entry.BestMove))
                        cleaned_moves.RemoveAt(j);

                cleaned_moves.Insert(0, entry.BestMove);
            }
        }

        if (NNUE_avx2)
            current_score = ValueNet.AccToOutput(ValueNet.acc, color);
        else
            current_score = eval.PestoEval(board, color);

        node_values[0] = !in_check ? current_score : 2;

        for (int current_depth = 1; current_depth <= depth; current_depth++)
        {
            root_depth = current_depth;

            //check if the current position is already in the Hash Table
            if (IsvalidEntry(key) == 1)
            {
                TTableEntry entry = GetInfoFromEntry(key);

                //order the last best move first
                for (int j = 0; j < cleaned_moves.Count; j++)
                    if (IsEqual(cleaned_moves[j], entry.BestMove))
                        cleaned_moves.RemoveAt(j);
           
                cleaned_moves.Insert(0, entry.BestMove);
            }

            if (current_depth >= 4 && Math.Abs(last_last_best) != 1) 
            {
                //if the current depth is larger then 4 reajust the window
                delta_a = -0.125f;
                window_a = add_p_value_to_wdl(last_last_best, delta_a); 
                delta_b = 0.125f;
                window_b = add_p_value_to_wdl(last_last_best, delta_b);
                alpha = window_a;
                //Console.WriteLine("Init Aspiration Window (high depth): alpha={0}; window_a={1}; window_b={2}; delta={3}", alpha, window_a, window_b, delta);

                //Console.WriteLine("last best {0} delta {1}\nwindow a {2} window b {3}", last_best, delta, window_a, window_b);
            }
            else
            {
                window_a = -2;
                window_b = 2;
                alpha = window_a;
                //Console.WriteLine("Init Aspiration Window (normal depth): alpha={0}; window_a={1}; window_b={2}; delta={3}", alpha, window_a, window_b, delta);
            }

            while (!stop)
            {
                movecount = 0;
                search_pv = true;

                foreach (int[] move in cleaned_moves)
                {
                    movecount++;
                    current_variation = new pv_out();

                    //play the move
                    board = MoveGenerator.PlayMove(board, color, move);

                    //play the move in the accumulator
                    ValueNet.update_acc_from_move(board, MoveGenerator.UnmakeMove);

                    //copy the unmake move into move undo
                    MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
                    Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);

                    //find if the current position is a terminal position
                    //determining the mate value 2 => not a terminal position , 0 => draw , 1 => mate for white , -1 => mate for black
                    int matingValue = MoveGenerator.Mate(board, othercolor);

                    //checking if the position is not a terminal node
                    if (matingValue != 2)
                    {
                        //if the position is a terminal node the value for the node is set to the mating value from the perspective of the current color
                        current_variation.Value = -matingValue;
                        current_variation.principalVariation.Insert(0, move);
                        //if the value is better than the currently best value return it because no value can be better or worse
                        if (alpha < current_variation.Value)
                        {
                            search_pv = false;
                            alpha = current_variation.Value;
                            pv = current_variation;
                        }
                    }
                    else
                    {
                        if (!MoveGenerator.CompleteCheck(board, othercolor))
                        {
                            //if the current depth is 1 perform a quiescent search
                            if (current_depth <= 1)
                            {
                                current_variation.Value = -quiescence_search(board, -window_b, -alpha, othercolor, NNUE_avx2, 0, 0);

                                current_variation.principalVariation.Add(move);
                            }
                            //else call the negamax function at the current depth minus 1
                            else
                            {
                                //perform a search
                                if (search_pv)
                                {
                                    current_variation = principal_variation_search(board, othercolor, current_depth - 1, 1, -window_b, -alpha, NNUE_avx2);
                                    current_variation.Value = -current_variation.Value;
                                    current_variation.principalVariation.Insert(0, move);
                                }
                                else
                                {
                                    current_variation.Value = -zero_window_search(board, othercolor, current_depth - 1, 1, -add_p_value_to_wdl(alpha, 0.0001f), -alpha, NNUE_avx2);

                                    if (stop)
                                    {
                                        MoveGenerator.fifty_move_rule = fifty_move_rule;
                                        //undo the current move
                                        board = MoveGenerator.UndoMove(board, MoveUndo);
                                        //copy the old accumulator back in the real accumulator
                                        Array.Copy(currentacc.Acc[1], ValueNet.acc.Acc[1], currentacc.Acc[1].Length);
                                        Array.Copy(currentacc.Acc[0], ValueNet.acc.Acc[0], currentacc.Acc[0].Length);

                                        break;
                                    }

                                    if (current_variation.Value > alpha)
                                    {
                                        current_variation = principal_variation_search(board, othercolor, current_depth - 1, 1, -window_b, -alpha, NNUE_avx2);
                                        current_variation.Value = -current_variation.Value;
                                        current_variation.principalVariation.Insert(0, move);
                                    }
                                }
                            }
                            //determine if the current move is better than the currently best move only if it is 
                            if (alpha < current_variation.Value && current_variation.Value != 2)
                            {
                                alpha = current_variation.Value;
                                pv = current_variation;

                                if (alpha > -1) 
                                    search_pv = false;
                            }
                        }
                    }
                    MoveGenerator.fifty_move_rule = fifty_move_rule;
                    //undo the current move
                    board = MoveGenerator.UndoMove(board, MoveUndo);
                    //add the en passent squares to the move generator
                    Array.Copy(EnPassent, MoveGenerator.EnPassent, EnPassent.Length);
                    //copy the old accumulator back in the real accumulator
                    Array.Copy(currentacc.Acc[1], ValueNet.acc.Acc[1], currentacc.Acc[1].Length);
                    Array.Copy(currentacc.Acc[0], ValueNet.acc.Acc[0], currentacc.Acc[0].Length);

                    if (stop || alpha >= window_b || alpha == 1)
                        break;
                }

                if (alpha <= window_a)
                {
                    delta_a *= 2;
                    delta_a -= 0.125f;
                    window_a = add_p_value_to_wdl(last_last_best, delta_a) + delta_a / 5;
                    alpha = window_a;
                }
                else if(alpha >= window_b)
                {
                    //order the move that caused the beta cutoff first
                    for (int j = 0; j < cleaned_moves.Count; j++)
                        if (IsEqual(cleaned_moves[j], pv.principalVariation[0]))
                            cleaned_moves.RemoveAt(j);

                    cleaned_moves.Insert(0, pv.principalVariation[0]);

                    delta_b *= 2;
                    delta_b += 0.125f;
                    alpha = window_a;
                    window_b = add_p_value_to_wdl(last_last_best, delta_b) + delta_b / 5;
                }
                else
                    break;
            }
            if (!stop)
            {
                Output = pv.principalVariation[0];

                //add the best move to the hash table
                AddToTable(Output, current_depth, alpha, key, 0, 0);
            }
            //after a finished search return the main informations 
            if (!stop)
            {
                if (alpha != 1 && alpha != -1)
                    Console.WriteLine("info depth {2} seldepth {3} nodes {1} nps {4} time {5} score cp {0} pv {6}", Math.Round(inverse_sigmoid(alpha, 4.2f) * 100), Nodecount, current_depth, current_depth + max_ply, (int)(((float)(Nodecount) * 1000) / (sw.ElapsedMilliseconds > 0 ? (float)sw.ElapsedMilliseconds : 1)), (int)(sw.ElapsedMilliseconds), variation_to_string(pv.principalVariation));
                else
                {
                    Console.WriteLine("info depth {2} seldepth {3} nodes {1} nps {4} time {5} score mate {0} pv {6}", ((pv.principalVariation.Count + 1) / 2) * alpha, Nodecount, current_depth, current_depth + max_ply, (int)(((float)(Nodecount) * 1000) / (sw.ElapsedMilliseconds > 0 ? (float)sw.ElapsedMilliseconds : 1)), (int)(sw.ElapsedMilliseconds), variation_to_string(pv.principalVariation));
                    if (depth < byte.MaxValue) break;
                }
            }
             
            if (stop)
            {
                Nodecount = 0;
                max_ply = 0;
                stop = false;
                break;
            }
            last_last_best = last_best;
            last_best = alpha;
            alpha = -2;
            max_ply = 0;
        }
        Nodecount = 0;
        //stop the stopwatch
        sw.Stop();
        sw.Reset();
        //stream_writer.Close();
        //return the best move
        if (Output.Length == 0)
            return iterative_deepening(board, color, 1, NNUE_avx2);
        else
            return Output;
    }
    public pv_out principal_variation_search(byte[,] board, byte color, int depth, int ply ,float alpha, float beta, bool NNUE_avx2)
    {
        //define the variables
        byte othercolor = (byte)(1 - color);
        bool found_legal_position = false, search_pv = true, in_check = false, two_fold_repetition = false, is_futile = false , improving = true , full_depth_search = false , fail_low = false , pruning_is_safe = false;
        int[] MoveUndo, BestMove = new int[0];
        int movecount = 0 , fifty_move_rule = MoveGenerator.fifty_move_rule , interesting_move_count , new_depth;
        float current_score = 0;
        Accumulator currentacc = new Accumulator(128);
        pv_out Output = new pv_out(), current_variation = new pv_out();
        Output.Value = alpha;
        TTableEntry entry = new TTableEntry(new int[0], 0, 0 , false , false);

        if (fifty_move_rule == 50 || stop)
        {
            Output.Value = 0;
            return Output;
        }

        //get the key for the position
        long key = ZobristHash(board, color);

        //threefold repetition
        if(is_in_fast_lookup(key))
        {
            if (repetition_count(key) == 2)
            {
                Output.Value = 0;
                return Output;
            }
            else if (repetition_count(key) == 1)
                two_fold_repetition = true;
        }

        int KeyValid = IsvalidEntry(key);

        if (KeyValid > -2)
        {
            entry = GetInfoFromEntry(key);

            if (KeyValid == 1)
            {
                //if the position has the right depth return the value of the position
                if (entry.depth >= depth)
                {
                    if (entry.Score >= beta && !entry.fail_low)
                    {
                        Output.Value = beta;
                        return Output;
                    }
                    if (entry.Score <= alpha && !entry.fail_high)
                    {
                        Output.Value = alpha;
                        return Output;
                    }
                }
            }
        }

        //calculate if the king is in check
        in_check = MoveGenerator.CompleteCheck(board, othercolor);

        List<int[]> Moves = MoveGenerator.ReturnPossibleMoves(board, color), CleanedMoves = new List<int[]>();

        //copy the accumulator for the current position
        Array.Copy(ValueNet.acc.Acc[0], currentacc.Acc[0], currentacc.Acc[0].Length);
        Array.Copy(ValueNet.acc.Acc[1], currentacc.Acc[1], currentacc.Acc[1].Length);

        //if the position is legal
        if (Moves != null)
        {
            //get only the legal moves
            foreach (int[] Move in Moves)
                if (Move.Length != 5 || !MoveGenerator.CastlingCheck(board, Move))
                    CleanedMoves.Add(Move);

            //sort the moves
            CleanedMoves = sort_moves(board, CleanedMoves, (byte)depth , color);
            interesting_move_count = capture_counter;
        }

        //the position is illegal
        else
        {
            Output.Value = -2;
            return Output;
        }

        //check if the current position is already in the Hash Table
        if (KeyValid == 1)
        {
            bool didpasstrought = false;

            //order the last best move first
            for (int j = 0; j < CleanedMoves.Count; j++)
                if (IsEqual(CleanedMoves[j], entry.BestMove))
                {
                    CleanedMoves.RemoveAt(j);
                    didpasstrought = true;
                    break;
                }

            if (didpasstrought)
                CleanedMoves.Insert(0, entry.BestMove);

            if (didpasstrought && board[entry.BestMove[2], entry.BestMove[3]] == 0 && entry.BestMove.Length != 5)
                interesting_move_count++;
        }

        //internal iterative deepening
        else if (depth >= 5)
        {
            float current_value = 0, best_value = -2;

            foreach (int[] move in CleanedMoves)
            {
                //play the current move on the board
                board = MoveGenerator.PlayMove(board, color, move);

                MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
                Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);

                //play the move in the accumulator
                ValueNet.update_acc_from_move(board, MoveGenerator.UnmakeMove);

                current_value = -principal_variation_search(board, othercolor, depth / 5, ply + 1, -beta, -best_value, NNUE_avx2).Value;

                if (current_value > best_value && current_value != 2)
                {
                    best_value = current_value;
                    BestMove = new int[move.Length];
                    Array.Copy(move, BestMove, move.Length);
                }

                MoveGenerator.fifty_move_rule = fifty_move_rule;
                //undo the current move
                board = MoveGenerator.UndoMove(board, MoveUndo);
                //copy the old accumulator back inthe real accumulator
                Array.Copy(currentacc.Acc[1], ValueNet.acc.Acc[1], currentacc.Acc[1].Length);
                Array.Copy(currentacc.Acc[0], ValueNet.acc.Acc[0], currentacc.Acc[0].Length);

                if (best_value >= beta)
                    break;
            }

            //sort the best move in front

            bool didpasstrougth = false;

            //order the last best move first
            for (int j = 0; j < CleanedMoves.Count; j++)
                if (IsEqual(CleanedMoves[j], BestMove))
                {
                    CleanedMoves.RemoveAt(j);
                    didpasstrougth = true;
                    break;
                }

            if (didpasstrougth)
                CleanedMoves.Insert(0, BestMove);

            if (didpasstrougth && board[BestMove[2], BestMove[3]] == 0 && BestMove.Length != 5)
                interesting_move_count++;
        }

        AddPositionToLookups(key);

        //get the current score
        if (!in_check)
        {
            if (NNUE_avx2)
                current_score = ValueNet.AccToOutput(ValueNet.acc, color);
            else
                current_score = eval.PestoEval(board, color);
        }

        /* update the value in the value array
         * if we are in check do not update the value*/

        node_values[ply] = !in_check ? current_score : 2;

        //set the improving flag high if the current value is an improvement
        improving = (ply >= 2 && node_values[ply] - node_values[ply - 2] > 0 || ply < 2) && !in_check;

        /*we should be able to prune branches only in specific cases
         * when we are not in check
         * and when the depth of the root is larger then 3
         */

        pruning_is_safe = !in_check && root_depth > 3;

        foreach (int[] move in CleanedMoves)
        {
            movecount++;

            //find futile moves
            if (pruning_is_safe && alpha > -1 && !is_futile && depth <= 7 && MoveGenerator.non_pawn_material)
            {
                if (movecount >= move_pruning(depth, improving))
                    is_futile = true;
                else if (inverse_sigmoid(current_variation.Value, 4.2f) + extended_futility_pruning_margin(depth) <= inverse_sigmoid(alpha, 4.2f) && Math.Abs(alpha) < 1)
                    is_futile = true;
            }

            //set the new depth
            new_depth = depth - 1;

            /*calculate depth reductions and depth extentions*/

            //check extention
            if (in_check)
                new_depth++;

            current_variation = new pv_out();

            //play the current move on the board
            board = MoveGenerator.PlayMove(board, color, move);

            MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
            Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);

            //play the move in the accumulator
            ValueNet.update_acc_from_move(board, MoveGenerator.UnmakeMove);

            //if the current depth is 1 do a quiescent search
            if (new_depth <= 0)
            {
                current_variation.Value = -quiescence_search(board, -beta, -alpha, othercolor, NNUE_avx2, 0 , ply + 1);
                current_variation.principalVariation.Add(move);
            }
            //else just call the function recursively
            else
            {
                if (search_pv)
                {
                    current_variation = principal_variation_search(board, othercolor, new_depth, ply + 1, -beta, -alpha, NNUE_avx2);
                    current_variation.Value = -current_variation.Value;
                    current_variation.principalVariation.Insert(0, move);
                }
                else
                {
                    //late move reduction
                    if (depth > 2 && movecount > interesting_move_count && false)
                    {
                        int decrease = reduction(depth, movecount, true);

                        if (!improving && !in_check)
                            decrease += 1;

                        int lmr_depth = Math.Max(Math.Min(new_depth, new_depth - decrease), 1);

                        if (lmr_depth == 0)
                            current_variation.Value = -quiescence_search(board, -add_p_value_to_wdl(alpha, 0.0001f), -alpha, othercolor, NNUE_avx2, 0, ply + 1);
                        else
                            current_variation.Value = -zero_window_search(board, othercolor, lmr_depth, ply + 1, -add_p_value_to_wdl(alpha, 0.0001f), -alpha, NNUE_avx2);

                        if (current_variation.Value == 2)
                        {
                            MoveGenerator.fifty_move_rule = fifty_move_rule;
                            //undo the current move
                            board = MoveGenerator.UndoMove(board, MoveUndo);
                            //copy the old accumulator back inthe real accumulator
                            Array.Copy(currentacc.Acc[1], ValueNet.acc.Acc[1], currentacc.Acc[1].Length);
                            Array.Copy(currentacc.Acc[0], ValueNet.acc.Acc[0], currentacc.Acc[0].Length);

                            continue;
                        }

                        if (current_variation.Value > alpha && lmr_depth < new_depth)
                            full_depth_search = true;
                    }
                    else
                        full_depth_search = true;

                    if(full_depth_search)
                    {
                        current_variation.Value = -zero_window_search(board, othercolor, new_depth, ply + 1, -add_p_value_to_wdl(alpha, 0.0001f), - alpha, NNUE_avx2);

                        full_depth_search = false;

                        if (current_variation.Value == 2)
                        {
                            MoveGenerator.fifty_move_rule = fifty_move_rule;

                            //undo the current move
                            board = MoveGenerator.UndoMove(board, MoveUndo);

                            //copy the old accumulator back inthe real accumulator
                            Array.Copy(currentacc.Acc[1], ValueNet.acc.Acc[1], currentacc.Acc[1].Length);
                            Array.Copy(currentacc.Acc[0], ValueNet.acc.Acc[0], currentacc.Acc[0].Length);

                            continue;
                        }
                    }

                    if (current_variation.Value > alpha)
                    {
                        current_variation = principal_variation_search(board, othercolor, new_depth, ply + 1, -beta, -alpha, NNUE_avx2);
                        current_variation.Value = -current_variation.Value;
                        current_variation.principalVariation.Insert(0, move);
                    }
                }
            }

            MoveGenerator.fifty_move_rule = fifty_move_rule;
            //undo the current move
            board = MoveGenerator.UndoMove(board, MoveUndo);
            //copy the old accumulator back inthe real accumulator
            Array.Copy(currentacc.Acc[1], ValueNet.acc.Acc[1], currentacc.Acc[1].Length);
            Array.Copy(currentacc.Acc[0], ValueNet.acc.Acc[0], currentacc.Acc[0].Length);

            //if the current score is not 2 the position is legal and therefore we have found a legal move
            if (current_variation.Value != 2)
            {
                found_legal_position = true;
                if (current_variation.Value > alpha)
                {
                    alpha = current_variation.Value;
                    BestMove = new int[move.Length];
                    Array.Copy(move, BestMove, move.Length);
                    Output = current_variation;

                    if (alpha > -1)
                        search_pv = false;
                }
            }
            //if the branch is not better then the currently best branch we can prune the other positions
            if (current_variation.Value >= beta && current_variation.Value != 2)
            {
                //store the killer move history moves and counter moves
                if (board[move[2], move[3]] == 0 && move.Length != 5) 
                {
                    update_killer_moves(move, depth);

                    update_history_moves(move, CleanedMoves, color, depth);

                    update_counter_moves(move, ply, color);
                }

                //add the best move to the hash table if the current depth is greater than the depth of the entry or thre is no entry in the hash table
                if (!stop && (KeyValid == -2 || entry.depth <= depth))
                    AddToTable(move, depth, beta, key , 1 , 0);

                RemovePositionFromLookups(key, !two_fold_repetition);

                Output.principalVariation = new List<int[]>();
                Output.Value = beta;
                return Output;
            }

            if (movecount >= interesting_move_count && is_futile && false) 
            {
                if (!found_legal_position)
                {
                    for (int i = movecount; i < CleanedMoves.Count; i++)
                    {
                        //play the current move on the board
                        board = MoveGenerator.PlayMove(board, color, CleanedMoves[i]);

                        //check if the move is legal
                        if (!MoveGenerator.CompleteCheck(board, othercolor))
                        {
                            found_legal_position = true;
                            //undo the current move
                            board = MoveGenerator.UndoMove(board, MoveGenerator.UnmakeMove);
                            break;
                        }

                        //undo the current move
                        board = MoveGenerator.UndoMove(board, MoveGenerator.UnmakeMove);
                    }
                }
                break;
            }
        }

        RemovePositionFromLookups(key, !two_fold_repetition);
        //if no move was legal return the score for mate
        if (!found_legal_position)
        {
            //mate
            if (in_check)
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
            //if we have not managed to exeed alpha we have not found the best move so we use the first move we searched
            if (BestMove.Length == 0)
            {
                fail_low = true;
                BestMove = CleanedMoves[0];
            }

            //add the best move to the hash table if the current depth is greater than the depth of the entry or thre is no entry in the hash table
            if (!stop && (KeyValid == -2 || entry.depth <= depth))
                AddToTable(BestMove, depth, alpha, key, 0, (byte)(fail_low ? 1 : 0));

            //return the best score
            return Output;
        }
    }
    public float zero_window_search(byte[,] board, byte color, int depth, int ply, float alpha, float beta, bool NNUE_avx2)
    {
        //define the variables
        float current_score = 0;
        byte othercolor = (byte)(1 - color);
        bool found_legal_position = false, in_check = MoveGenerator.CompleteCheck(board, othercolor), two_fold_repetition = false , full_depth_search = false;
        int movecount = 0 , fifty_move_rule = MoveGenerator.fifty_move_rule , interesting_move_count , new_depth = 0;
        int[] MoveUndo, BestMove = new int[0];
        bool is_futile = false , improving = false , pruning_is_safe = false;
        Accumulator currentacc = new Accumulator(128);
        TTableEntry entry = new TTableEntry(new int[0], 0, 0, false, false);

        /*if (in_check)
            stream_writer.WriteLine("check;{0}", generate_fen_from_position(board, color, fifty_move_rule));*/

        if (fifty_move_rule == 50 || stop)
            return 0;

        //get the key for the position
        long key = ZobristHash(board, color);

        //threefold repetition
        if (is_in_fast_lookup(key))
        {
            if (repetition_count(key) == 2)
                return 0;
            else if (repetition_count(key) == 1)
                two_fold_repetition = true;
        }

        int KeyValid = IsvalidEntry(key);

        if (KeyValid > -2)
        {
            entry = GetInfoFromEntry(key);

            if (KeyValid == 1)
            {
                //if the position has the right depth we can use the value of the position
                if (entry.depth >= depth)
                {
                    //if the score is larger or equal to beta we can return beta
                    if (entry.Score >= beta && !entry.fail_low) 
                        return beta;
                    //else if the score is certain and it is smaller then alpha we have an alpha cutoff
                    if (entry.Score <= alpha && !entry.fail_high)
                        return alpha;
                }
            }
        }

        if (!in_check)
        {
            if (NNUE_avx2)
                current_score = ValueNet.AccToOutput(ValueNet.acc, color);
            else
                current_score = eval.PestoEval(board, color);
        }

        List<int[]> Moves = MoveGenerator.ReturnPossibleMoves(board, color), CleanedMoves = new List<int[]>();
        //copy the accumulator for the current position
        Array.Copy(ValueNet.acc.Acc[0], currentacc.Acc[0], currentacc.Acc[0].Length);
        Array.Copy(ValueNet.acc.Acc[1], currentacc.Acc[1], currentacc.Acc[1].Length);

        //if the position is legal
        if (Moves != null)
        {
            //get only the legal moves
            foreach (int[] Move in Moves)
                if (Move.Length != 5 || !MoveGenerator.CastlingCheck(board, Move))
                    CleanedMoves.Add(Move);

            //sort the moves for most valuable victim vs least valuable attacker
            CleanedMoves = sort_moves(board, CleanedMoves, (byte)depth, color);
            interesting_move_count = capture_counter;

            //check if the current position is already in the Hash Table
            if (KeyValid == 1)
            {
                bool didpasstrought = false;

                //order the last best move first
                for (int j = 0; j < CleanedMoves.Count; j++)
                    if (IsEqual(CleanedMoves[j], entry.BestMove))
                    {
                        CleanedMoves.RemoveAt(j);
                        didpasstrought = true;
                        break;
                    }

                if (didpasstrought)
                    CleanedMoves.Insert(0, entry.BestMove);

                //if the tt move is not a capture there is one more interesting move
                if (didpasstrought && board[entry.BestMove[2], entry.BestMove[3]] == 0 && entry.BestMove.Length != 5)
                    interesting_move_count++;
            }
        }
        //the position is illegal
        else
            return -2;

        /* update the value in the value array
         * if we are in check do not update the value*/
 
        node_values[ply] = !in_check ? current_score : 2;

        //set the improving flag high if the current value is an improvement
        improving = (ply >= 2 && node_values[ply] - node_values[ply - 2] > 0 || ply < 2) && !in_check;

        /*we should be able to prune branches only in specific cases
         * when we are not in check
         * and when the depth of the root is larger then 3
         */

        pruning_is_safe = !in_check && root_depth > 3;

        /*Razoring
         * 
         * if the current score is really bad,
         * we try a quiescence search to look if a tactical sequence can make up for the bad score
         * if this is not the case we just prune 
         */

        if (depth <= 7 && pruning_is_safe && inverse_sigmoid(current_score, 4.2f) < inverse_sigmoid(alpha, 4.2f) - razoring_margin(depth, improving) && alpha != 2 && false) 
        {
            float test_value = quiescence_search(board, add_p_value_to_wdl(alpha, -0.0001f), alpha, color, NNUE_avx2, 0, ply + 1);

            if (test_value < alpha && test_value != 2)
                return test_value;
        }

        //Reverse Futility Pruning
        if (depth <= 7 && pruning_is_safe && add_p_value_to_wdl(current_score, -reverse_futility_pruning_margin(depth, improving)) >= beta && false)
        { 
            return beta;
        }


        /* Null Move Pruning
         * 
         * when the position looks to be larger the beta,
         * we look if it is still better when we give the opponent two moves in a row
         * 
         * we want to avoid zugzwang because it is the only case in which 
         * the null move observation (you can always make the evaluation better when you play the best move)
         * does not work, so we do not prune when there are only pawns because there is a larger probability
         * for zugzwang in these positions
         * 
         * else we want to avoid searching two null moves in a row
         */

        if (depth >= 3 && current_score >= beta && pruning_is_safe && MoveGenerator.non_pawn_material && !null_move_pruning[ply - 1] && (ply < 2 || !null_move_pruning[ply - 2]) && false) 
        {
            float nmp_score = 0;

            /* calculate the depth for the null move search
             * 
             * 1) the base depth reduction factor is 4
             * 
             * 2) else the depth gets reduced by 1/6
             * 
             * 3) the larger the delta between the standing pat and beta the more we can reduce
             */

            int null_move_search_depth = depth - (4 + depth / 6 + Math.Min(3, (int)((inverse_sigmoid(current_score , 4.2f) - inverse_sigmoid(beta, 4.2f)) / 4)));

            //Make null Move

            if (null_move_search_depth <= 0)
                nmp_score = -quiescence_search(board, -beta, -alpha, othercolor, NNUE_avx2, 0, ply + 1);
            else
            {
                //add the null move search to the table
                null_move_pruning[ply] = true;

                nmp_score = -zero_window_search(board, othercolor, null_move_search_depth, ply + 1, -beta, -alpha, NNUE_avx2);

                //remove the null move search from the table
                null_move_pruning[ply] = false;
            }

            //Unmake the null move

            if (nmp_score >= beta && nmp_score != 2) 
            {
                //if the current value is not a mate or the depthis low return the value 
                if (Math.Abs(beta) != 1 && depth <= 17)
                    return beta;

                //else make a verification search to be sure the score is valid

                //search from the own perspective
                if (null_move_search_depth <= 0)
                    nmp_score = -quiescence_search(board, alpha, beta, color, NNUE_avx2, 0, ply + 1);
                else
                    nmp_score = -zero_window_search(board, color, null_move_search_depth, ply + 1, alpha, beta, NNUE_avx2);

                //if the value is still larger then beta return beta
                if (nmp_score >= beta)
                    return beta;
            }
        }

        AddPositionToLookups(key);

        foreach (int[] move in CleanedMoves)
        {
            movecount++;

            //find futile moves
            if (pruning_is_safe && alpha > -1 && !is_futile && depth <= 7 && MoveGenerator.non_pawn_material)
            {
                if (movecount >= move_pruning(depth, improving))
                    is_futile = true;
                else if (inverse_sigmoid(current_score, 4.2f) + extended_futility_pruning_margin(depth) <= inverse_sigmoid(alpha, 4.2f) && Math.Abs(alpha) < 1)
                    is_futile = true;
            }

            //set the new depth
            new_depth = depth - 1;

            /*calculate depth reductions and depth extentions*/

            //check extention
            if (in_check)
                new_depth++;

            //play the current move on the board
            board = MoveGenerator.PlayMove(board, color, move);

            MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
            Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);

            //play the move in the accumulator
            ValueNet.update_acc_from_move(board,MoveGenerator.UnmakeMove);

            //if the current depth is 1 do a quiescent search
            if (new_depth <= 0)
                current_score = -quiescence_search(board, -beta, -alpha, othercolor, NNUE_avx2, 0 , ply + 1);
            //else just call the function recursively
            else
            {
                //late move reduction
                if (depth > 2 && movecount > interesting_move_count && false) 
                {
                    int decrease = reduction(depth, movecount, false);

                    if (!improving && !in_check)
                        decrease += 1;

                    int lmr_depth = Math.Max(Math.Min(new_depth, new_depth - decrease), 1);

                    if (lmr_depth == 0)
                        current_score = -quiescence_search(board, -beta, -alpha, othercolor, NNUE_avx2, 0, ply + 1);
                    else
                        current_score = -zero_window_search(board, othercolor, lmr_depth, ply + 1, -beta, -alpha, NNUE_avx2);

                    if (current_score == 2)
                    {
                        MoveGenerator.fifty_move_rule = fifty_move_rule;
                        //undo the current move
                        board = MoveGenerator.UndoMove(board, MoveUndo);
                        //copy the old accumulator back in the real accumulator
                        Array.Copy(currentacc.Acc[1], ValueNet.acc.Acc[1], currentacc.Acc[1].Length);
                        Array.Copy(currentacc.Acc[0], ValueNet.acc.Acc[0], currentacc.Acc[0].Length);

                        continue;
                    }

                    if (current_score > alpha && lmr_depth < new_depth)
                        full_depth_search = true;
                }
                else
                    full_depth_search = true;

                if(full_depth_search)
                {
                    current_score = -zero_window_search(board, othercolor, new_depth, ply + 1, -beta, -alpha, NNUE_avx2);

                    full_depth_search = false;

                    if (current_score == 2)
                    {
                        MoveGenerator.fifty_move_rule = fifty_move_rule;
                        //undo the current move
                        board = MoveGenerator.UndoMove(board, MoveUndo);
                        //copy the old accumulator back inthe real accumulator
                        Array.Copy(currentacc.Acc[1], ValueNet.acc.Acc[1], currentacc.Acc[1].Length);
                        Array.Copy(currentacc.Acc[0], ValueNet.acc.Acc[0], currentacc.Acc[0].Length);

                        continue;
                    }
                }
            }

            found_legal_position = current_score != 2 || found_legal_position;

            MoveGenerator.fifty_move_rule = fifty_move_rule;

            //undo the current move
            board = MoveGenerator.UndoMove(board, MoveUndo);
            //copy the old accumulator back inthe real accumulator
            Array.Copy(currentacc.Acc[1], ValueNet.acc.Acc[1], currentacc.Acc[1].Length);
            Array.Copy(currentacc.Acc[0], ValueNet.acc.Acc[0], currentacc.Acc[0].Length);

            //if the branch is not better then the currently best branch we can prune the other positions
            if (current_score >= beta && current_score != 2)
            {
                //store the killer move history moves and counter moves
                if (board[move[2], move[3]] == 0 && move.Length != 5)
                {
                    update_killer_moves(move, depth);

                    update_history_moves(move, CleanedMoves, color, depth);

                    update_counter_moves(move, ply , color);
                }

                //add the best move to the hash table if the current depth is greater than the depth of the entry or thre is no entry in the hash table
                if (!stop && (KeyValid == -2 || entry.depth <= depth))
                    AddToTable(move, depth, beta, key, 1, 0);

                RemovePositionFromLookups(key, !two_fold_repetition);

                return beta;
            }

            if (movecount >= interesting_move_count && is_futile && false)
            {
                if (!found_legal_position)
                {
                    for (int i = movecount; i < CleanedMoves.Count; i++)
                    {
                        //play the current move on the board
                        board = MoveGenerator.PlayMove(board, color, CleanedMoves[i]);

                        //check if the move is legal
                        if (!MoveGenerator.CompleteCheck(board, othercolor))
                        {
                            found_legal_position = true;
                            //undo the current move
                            board = MoveGenerator.UndoMove(board, MoveGenerator.UnmakeMove);
                            break;
                        }

                        //undo the current move
                        board = MoveGenerator.UndoMove(board, MoveGenerator.UnmakeMove);
                    }
                }
                break;
            }
        }

        RemovePositionFromLookups(key, !two_fold_repetition);

        //if no move was legal return the score for a terminal node
        if (!found_legal_position)
        { 
            //mate
            if (in_check)
                return -1;
            //stalemate
            else
                return 0;
        }
        else
        {
            //add the best move to the hash table if there is no entry in the hash table
            if (!stop && (KeyValid == -2 || entry.depth <= depth))
                AddToTable(CleanedMoves[0], depth, alpha, key, 0, 1);

            return alpha;
        }
    }
    public float quiescence_search(byte[,] board, float alpha, float beta, byte color, bool NNUE_avx2, int depth , int ply)
    {
        if (MoveGenerator.fifty_move_rule == 50 || stop)
            return 0;

        //look for repetitions
        if (depth == 0)
        {
            //get the key for the position
            long key = ZobristHash(board, color);

            //threefold repetition
            if (is_in_fast_lookup(key) && repetition_count(key) == 2)
                return 0;
        }

        //define the variables
        Nodecount++;
        float standing_pat = -2, current_score = 0;
        byte othercolor = (byte)(1 - color);
        bool in_check = MoveGenerator.CompleteCheck(board, othercolor) , legal_move = false;
        int[] MoveUndo;
        int move_count = 0;
        List<int[]> Moves, CleanedMoves = new List<int[]>();
        Accumulator currentacc = new Accumulator(128);

        //copy the accumulator for the current position
        Array.Copy(ValueNet.acc.Acc[0], currentacc.Acc[0], currentacc.Acc[0].Length);
        Array.Copy(ValueNet.acc.Acc[1], currentacc.Acc[1], currentacc.Acc[1].Length);

        //if we are in check look for  other moves
        if (in_check)
            Moves = MoveGenerator.ReturnPossibleMoves(board, color);
        //else just look for captures
        else
            Moves = MoveGenerator.ReturnPossibleCaptures(board, color);

        //if the position is legal
        if (Moves != null)
        {

            //if we are in check standing pat is no allowed because we search all moves and not only captures
            if (!in_check)
            {
                if (NNUE_avx2)
                    standing_pat = ValueNet.AccToOutput(ValueNet.acc, color);
                else
                    standing_pat = eval.PestoEval(board, color);
            }

            //if the branch is not better then the currently best branch we can prune the other positions
            if (standing_pat >= beta)
                return beta;

            //delta pruning
            if (inverse_sigmoid(standing_pat, 4.2f) < inverse_sigmoid(alpha , 4.2f) - 9 && !in_check)
                return alpha;

            if (standing_pat > alpha)
                alpha = standing_pat;

            //if the position is quiet return the evaluation
            if (Moves.Count == 0)
            {
                max_ply = Math.Max(ply, max_ply);

                if (in_check)
                {
                    int mate = MoveGenerator.Mate(board, color);

                    if (mate != 2)
                        return mate;
                }

                return alpha;
            }

            //get only the legal moves
            if (in_check)
            {
                foreach (int[] Move in Moves)
                    if (Move.Length != 5 || !MoveGenerator.CastlingCheck(board, Move))
                        CleanedMoves.Add(Move);
            }
            else
                CleanedMoves = Moves;

            //sort the moves for most valuable victim vs least valuable attacker
            CleanedMoves = sort_moves(board, CleanedMoves, (byte)depth, color);
        }

        //the position is illegal
        else return -2;

        foreach (int[] Move in CleanedMoves)
        {
            move_count++;

            //play the current move on the board
            board = MoveGenerator.PlayMove(board, color, Move);
            MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
            Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);
            //play the move in the accumulator
            ValueNet.update_acc_from_move(board, MoveGenerator.UnmakeMove);

            //calls itself recursively
            current_score = -quiescence_search(board, -beta, -alpha, othercolor, NNUE_avx2, depth + 1, ply + 1);

            //undo the current move
            board = MoveGenerator.UndoMove(board, MoveUndo);
            //copy the old accumulator back inthe real accumulator
            Array.Copy(currentacc.Acc[1], ValueNet.acc.Acc[1], currentacc.Acc[1].Length);
            Array.Copy(currentacc.Acc[0], ValueNet.acc.Acc[0], currentacc.Acc[0].Length);


            //if the current score is not 2 the position is not illegal and therefore we have found a legal move
            if (current_score != 2)
            {
                legal_move = true;

                if (current_score > alpha)
                    alpha = current_score;

                //if the branch is not better then the currently best branch we can prune the other positions
                if (current_score >= beta && current_score != 2)
                    return beta;
            }
        }

        if (!legal_move)
        {
            int mate = MoveGenerator.Mate(board, color);

            if (mate != 2)
                return mate;
            else
                return alpha;
        }
        else
            //return the best score
            return alpha;    
    }
    public alpha_beta_output selfplay_iterative_deepening(byte[,] board, byte color, int depthPly, bool NNUE_avx2)
    {
        //initialize the variables
        int[] MoveUndo;
        List<int[]> Moves = MoveGenerator.ReturnPossibleMoves(board, color), CleanedMoves = new List<int[]>();
        float alpha = -2, currentScore = -2;
        byte othercolor = (byte)(1 - color);
        bool search_pv = true, check = MoveGenerator.CompleteCheck(board, othercolor);
        int movecount = 0, capture_count = 0, fifty_move_rule = MoveGenerator.fifty_move_rule;
        Accumulator currentacc = new Accumulator(128);
        alpha_beta_output Output = new alpha_beta_output();
        //get the key for the position
        long key = ZobristHash(board, color);
        //start the stopwatch
        sw.Start();
        sw.Start();
        //the the accumulator position to the starting position
        ValueNet.set_acc_from_position(board);
        //copy the accumulator for the current position
        Array.Copy(ValueNet.acc.Acc[0], currentacc.Acc[0], currentacc.Acc[0].Length);
        Array.Copy(ValueNet.acc.Acc[1], currentacc.Acc[1], currentacc.Acc[1].Length);

        //get only the legal moves
        foreach (int[] Move in Moves)
            if (Move.Length != 5 || !MoveGenerator.CastlingCheck(board, Move))
                CleanedMoves.Add(Move);

        //sort the moves for most valuable victim vs least valuable attacker
        CleanedMoves = sort_moves(board, CleanedMoves, (byte)depthPly, color);
        capture_count = capture_counter;

        //perform i searches, i being the depth 
        for (int current_depth = 1; current_depth <= depthPly; current_depth++)
        {
            //check if the current position is already in the Hash Table
            if (IsvalidEntry(key) == 1)
            {
                TTableEntry entry = GetInfoFromEntry(key);

                //order the last best move first
                for (int j = 0; j < CleanedMoves.Count; j++)
                    if (IsEqual(CleanedMoves[j], entry.BestMove))
                        CleanedMoves.RemoveAt(j);

                CleanedMoves.Insert(0, entry.BestMove);

            }
            foreach (int[] move in CleanedMoves)
            {
                movecount++;
                //play the move
                board = MoveGenerator.PlayMove(board, color, move);
                //play the move in the accumulator
                ValueNet.update_acc_from_move(board, MoveGenerator.UnmakeMove);
                //copy the unmake move into move undo
                MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
                Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);

                //find if the current position is a terminal position
                //determining the mate value 2 => not a terminal position , 0 => draw , 1 => mate for white , -1 => mate for black
                int matingValue = MoveGenerator.Mate(board, othercolor);
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
                    if (current_depth == 1)
                    {
                        currentScore = -quiescence_search(board, -2, -alpha, othercolor, NNUE_avx2, 0 , 0);
                        Nodecount++;
                    }
                    //else call the negamax function at the current depth minus 1
                    else
                    {
                        if (search_pv)
                            currentScore = -principal_variation_search(board, othercolor, current_depth - 1,0, -2, -alpha, NNUE_avx2).Value;
                        else
                        {
                            currentScore = -zero_window_search(board, othercolor, current_depth - 1, 0, -add_p_value_to_wdl(alpha, 0.0001f), -alpha, NNUE_avx2);

                            if (currentScore == 2)
                            {
                                MoveGenerator.fifty_move_rule = fifty_move_rule;
                                //undo the current move
                                board = MoveGenerator.UndoMove(board, MoveUndo);
                                //copy the old accumulator back inthe real accumulator
                                Array.Copy(currentacc.Acc[1], ValueNet.acc.Acc[1], currentacc.Acc[1].Length);
                                Array.Copy(currentacc.Acc[0], ValueNet.acc.Acc[0], currentacc.Acc[0].Length);

                                continue;
                            }

                            if (stop)
                            {
                                MoveGenerator.fifty_move_rule = fifty_move_rule;
                                //undo the current move
                                board = MoveGenerator.UndoMove(board, MoveUndo);
                                //copy the old accumulator back inthe real accumulator
                                Array.Copy(currentacc.Acc[1], ValueNet.acc.Acc[1], currentacc.Acc[1].Length);
                                Array.Copy(currentacc.Acc[0], ValueNet.acc.Acc[0], currentacc.Acc[0].Length);

                                break;
                            }

                            if (currentScore > alpha)
                                currentScore = principal_variation_search(board, othercolor, current_depth - 1,0, -2, -alpha, NNUE_avx2).Value;
                        }
                    }
                    //determine if the current move is better than the currently best move only if it is legal
                    if (alpha < currentScore && currentScore != 2)
                    {
                        if (depthPly == current_depth)
                        {
                            Output.Scores = new List<float>();
                            Output.Scores.Add(currentScore);
                            Output.movelist = new List<int[]>();
                            Output.movelist.Add(move);
                        }
                        alpha = currentScore;
                        search_pv = false;
                    }
                }
                //undo the current move
                board = MoveGenerator.UndoMove(board, MoveUndo);

                //copy the old accumulator back inthe real accumulator
                Array.Copy(currentacc.Acc[1], ValueNet.acc.Acc[1], currentacc.Acc[1].Length);
                Array.Copy(currentacc.Acc[0], ValueNet.acc.Acc[0], currentacc.Acc[0].Length);
            }
            //stop and restart the stopwatch
            alpha = -2;
            currentScore = -2;
            Nodecount = 0;
            max_ply = 0;
            search_pv = true;
            movecount = 0;
        }
        //return the best move
        return Output;
    }
    public void AddToTable(int[] Move, int depth, float Value, long key , byte beta_cutoff , byte alpha_cutoff)
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

        //save the depth and the beta cutoff
        Log[0] = (byte)(depth + (byte)(beta_cutoff << 7));

        //save the evaluation
        for (int i = 0; i < 4; i++)
            Log[i + 1] = BitConverter.GetBytes(Value)[i];

        //save the move
        for (int i = 5; i < 5 + Move.Length; i++)
            Log[i] = (byte)(Move[i - 5] + 1);
        
        //add the flag for the alpha cutoff at the last index of the move
        Log[9] += (byte)(alpha_cutoff << 7);

        byte[] keyArray = BitConverter.GetBytes(key);

        for (int i = 0; i < 18; i++)
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
    public TTableEntry GetInfoFromEntry(long key)
    {
        int index = (int)(key % HashTable.GetLength(0));
        byte depth = HashTable[index, 0];
        bool beta_cutoff = depth >= 128;
        if (beta_cutoff) depth -= 128;
        byte[] EvalParts = new byte[4];
        EvalParts[0] = HashTable[index, 1];
        EvalParts[1] = HashTable[index, 2];
        EvalParts[2] = HashTable[index, 3];
        EvalParts[3] = HashTable[index, 4];
        float eval = BitConverter.ToSingle(EvalParts);
        int Movesize = 5;

        //get the flag for the alpha cutoff
        bool alpha_cutoff = HashTable[index, 9] >= 128;

        //remove the flag temporarly
        if (alpha_cutoff)
            HashTable[index, 9] -= 128;

        //collect the move
        if (HashTable[index, 9] == 0)
            Movesize--;

        int[] Move = new int[Movesize];

        for (int i = 5; i < 5 + Movesize; i++)
            if (HashTable[index, i] != 0)
                Move[i - 5] = HashTable[index, i] - 1;
            else
                Move[i - 5] = 0;

        //add the flag back in
        if (alpha_cutoff)
            HashTable[index, 9] += 128;

        return new TTableEntry(Move, eval, depth , beta_cutoff , alpha_cutoff);
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
    public void AddPositionToLookups(long Position)
    { 
        //add to fast lookup
        repetion_lookup[Position % repetion_lookup.Length] = true;

        //add to move array
        repetitions[move_counter] = Position;

        move_counter++;
    }
    public bool is_in_fast_lookup(long Position)
    {
        return repetion_lookup[Position % repetion_lookup.Length];
    }
    public int repetition_count(long Position)
    {
        int count = 0;
        for (int i = 0; i <= move_counter; i++)
        {
            if (repetitions[i] == Position)
                count++;
        }
        return count;
    }
    public byte[][,] PlayGameFromMoves(byte[,] InputBoard, byte color, int[][] Moves, bool TreeUpdate)
    {
        MoveGenerator.fifty_move_rule = 0;
        move_counter = 0;
        long key = 0;
        for (int i = 0; i < Moves.Length; i++)
        {
            key = ZobristHash(InputBoard, color);

            AddPositionToLookups(key);

            InputBoard = MoveGenerator.PlayMove(InputBoard, color, Moves[i]);

            if(MoveGenerator.fifty_move_rule == 0)
            {
                move_counter = 0;
                RepetitionTable[] repetitions = new RepetitionTable[307];
                repetion_lookup = new bool[ushort.MaxValue];
            }

            color = (byte)(1 - color);
        }

        key = ZobristHash(InputBoard, color);

        AddPositionToLookups(key);

        byte[,] ColorOut = new byte[1, 1];
        ColorOut[0, 0] = color;
        return new byte[2][,] { InputBoard, ColorOut };
    }
    public void RemovePositionFromLookups(long Position , bool both)
    {
        //remove from fast lookup
        if (both)
            repetion_lookup[Position % repetion_lookup.Length] = true;

        //derease the move counter
        move_counter--;
    }
    public long ZobristHash(byte[,] InputBoard, byte color)
    {
        long Output = (1 - color) * BlackToMove;

        for (int i = 1; i < 9; i++)
            for (int j = 1; j < 9; j++)
                Output ^= PieceHashes[InputBoard[i, j]][i, j];

        return Output;
    }
    public List<int[]> sort_moves(byte[,] InputBoard, List<int[]> Moves , byte depthPly , byte color)
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
                if (IsEqual(counter_moves[color][Move[0], Move[1], Move[2], Move[3]], Move))
                    currentPieceValue += 100;

                currentPieceValue += history_moves[color][Move[0], Move[1], Move[2], Move[3]];
            }
            else
            {
                capture_counter++;
                currentPieceValue += 700;
            }

            //sort the moves
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
                    else if (i == Values.Count - 1)
                    {
                        Values.Add(currentPieceValue);
                        SortedMoves.Add(Move);
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
                if (victimvalue != 0)
                    MVVLVA_array[attacker, victim] = (byte)(attackervalue + victimvalue);
            }
        }
    }
    public void update_counter_moves(int[] move , int ply , byte color)
    {
        if (!null_move_pruning[ply - 1])
            counter_moves[color][move[0], move[1], move[2], move[3]] = move;
    }
    public void update_killer_moves(int[] move , int depth)
    {
        if (!IsEqual(move, killer_moves[0, depth]))
        {
            if (killer_moves[0, depth] != null)
            {
                killer_moves[1, depth] = new int[killer_moves[0, depth].Length];
                Array.Copy(killer_moves[0, depth], killer_moves[1, depth], killer_moves[0, depth].Length);
            }

            killer_moves[0, depth] = move;
        }
    }
    public void update_history_moves(int[] move, List<int[]> movelist, byte color , int depth)
    {
        foreach(int[] researched_move in movelist)
        {
            if (IsEqual(researched_move, move))
                break;

            history_moves[color][researched_move[0], researched_move[1], researched_move[2], researched_move[3]] = history_score_update(history_moves[color][researched_move[0], researched_move[1], researched_move[2], researched_move[3]] , -depth * depth);
        }

        history_moves[color][move[0], move[1], move[2], move[3]] = history_score_update(history_moves[color][move[0], move[1], move[2], move[3]], depth * depth);
    }
    public float history_score_update(float current_score , float margin)
    {
        float margin_sign = margin > 0 ? 1 : -1, score_max_delta = 100 - margin_sign * current_score, max_margin = 100;

        return current_score + Math.Max(Math.Min(margin, 100), -100) * (score_max_delta / max_margin);
    }
    public int reduction_(int depthPly , int movecount , bool pv_node)
    {
        double multiplier = 1;
        if (pv_node) multiplier = 2 / 3;

        return (byte)(multiplier * (Math.Sqrt(depthPly - 1) + Math.Sqrt(movecount - 1)));
    }
    public int reduction(int depth, int movecount, bool pv_node)
    {
        if (movecount > 2 && !pv_node)
            return (byte)(0.8f + (Math.Log(depth) * Math.Log(movecount)) / 2.25f);
        else if (movecount > 3 && pv_node)
            return (byte)(Math.Log(depth) * Math.Log(movecount) / 2.25f - 0.2f);
        else
            return 0;
    }
    public float razoring_margin(int depth , bool improving)
    {
        float start_coefficient = !improving ? 2.75f : 4.25f;

        return start_coefficient + 3 * depth * depth;
    }
    public float reverse_futility_pruning_margin(float depth , bool improving)
    {
        float negator = improving ? 0 : 0.5f;

        return 1.75f * (depth - negator);
    }
    public float extended_futility_pruning_margin(float depth)
    {
        return 2 * depth + 1.5f;
    }
    public int move_pruning(int depthPly , bool improving)
    {
        int divisor = improving ? 1 : 2;

        return 3 + depthPly * depthPly / divisor;
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
    public void display_sorting()
    {
        int complete_count = 0;
        foreach (int count in sorting_counter)
            complete_count += count;
        for (int i = 0; i < 10; i++)
            Console.WriteLine("the count at {0} made up for {1}% of the {2} entries", i + 1, (sorting_counter[i] * 100) / complete_count, complete_count);
        sorting_counter = new int[300];
    }
    public float inverse_sigmoid(float input , float size)
    {
        int sign = input < 0 ? -1 : 1;

        return (float)Math.Sqrt(input * input / (1 - input * input)) * size * sign;
    }
    public float sigmoid(float input, float size)
    {
        return (input / size) / (float)Math.Sqrt((input / size) * (input / size) + 1);
    }
    public float add_p_value_to_wdl(float input , float to_add)
    {
        return sigmoid(inverse_sigmoid(input, 4.2f) + to_add, 4.2f);
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
    public void DisplayCurrentBoard(byte[,] InputBoard , byte color)
    {
        string spacer = "+---+---+---+---+---+---+---+---+";
        string backrow = "  a   b   c   d   e   f   g   h";
        string[] rows = new string[8];
        for (int i = 1; i < 9; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                if (InputBoard[i, j + 1] != 0)
                {
                    switch (InputBoard[i, j + 1])
                    {
                        case 0b00000001:
                            rows[j] += "| p ";
                            break;
                        case 0b00000011:
                            rows[j] += "| p ";
                            break;
                        case 0b00000010:
                            rows[j] += "| p ";
                            break;
                        case 0b00000100:
                            rows[j] += "| n ";
                            break;
                        case 0b00000101:
                            rows[j] += "| b ";
                            break;
                        case 0b00000110:
                            rows[j] += "| k ";
                            break;
                        case 0b00000111:
                            rows[j] += "| k ";
                            break;
                        case 0b00001000:
                            rows[j] += "| q ";
                            break;
                        case 0b00001001:
                            rows[j] += "| r ";
                            break;
                        case 0b00001010:
                            rows[j] += "| r ";
                            break;
                        case 0b00010001:
                            rows[j] += "| P ";
                            break;
                        case 0b00010010:
                            rows[j] += "| P ";
                            break;
                        case 0b00010011:
                            rows[j] += "| P ";
                            break;
                        case 0b00010100:
                            rows[j] += "| N ";
                            break;
                        case 0b00010101:
                            rows[j] += "| B ";
                            break;
                        case 0b00010110:
                            rows[j] += "| K ";
                            break;
                        case 0b00010111:
                            rows[j] += "| K ";
                            break;
                        case 0b00011000:
                            rows[j] += "| Q ";
                            break;
                        case 0b00011001:
                            rows[j] += "| R ";
                            break;
                        case 0b00011010:
                            rows[j] += "| R ";
                            break;
                    }
                }
                else
                    rows[j] += "|   ";

            }
        }
        for (int i = 7; i >= 0; i--)
        {
            Console.WriteLine(spacer + "\n" + rows[i] + "| " + (i + 1));
        }
        Console.WriteLine(spacer + "\n" + backrow);

        if (color == 0)
            Console.WriteLine("Black To Play");
        else
            Console.WriteLine("White To Play");
    }
    public string make_fen(byte[,] board , byte color)
    {
        return "";
    }
    public string generate_fen_from_position(byte[,] position, byte color, int fifty_move_rule)
    {
        string fen_output = "";
        int en_passent_x = 0, en_passent_y = 0;
        bool castle_W_K = false, castle_W_Q = false, castle_B_K = false, castle_B_Q = false;
        int square_count = 0;
        char[] Numbers = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8' };
        char[] Letters = new char[] { '0', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h' };

        for (int j = 8; j > 0; j--)
        {
            for (int i = 1; i < 9; i++)
            {
                if (position[i, j] == 0)
                    square_count++;
                else if (square_count != 0)
                {
                    fen_output += Convert.ToString(square_count);
                    square_count = 0;
                }
                switch (position[i, j])
                {
                    case 0b00000001:
                        fen_output += "p";
                        break;
                    case 0b00000010:
                        fen_output += "p";
                        en_passent_x = i;
                        en_passent_y = j;
                        break;
                    case 0b00000011:
                        fen_output += "p";
                        break;
                    case 0b00000100:
                        fen_output += "n";
                        break;
                    case 0b00000101:
                        fen_output += "b";
                        break;
                    case 0b00000110:
                        fen_output += "k";
                        break;
                    case 0b00000111:
                        fen_output += "k";
                        break;
                    case 0b00001000:
                        fen_output += "q";
                        break;
                    case 0b00001001:
                        fen_output += "r";
                        if (i == 1)
                            castle_B_Q = true;
                        else if (i == 8)
                            castle_B_K = true;
                        break;
                    case 0b00001010:
                        fen_output += "r";
                        break;

                    case 0b00010001:
                        fen_output += "P";
                        break;
                    case 0b00010010:
                        fen_output += "P";
                        en_passent_x = i;
                        en_passent_y = j;
                        break;
                    case 0b00010011:
                        fen_output += "P";
                        break;
                    case 0b00010100:
                        fen_output += "N";
                        break;
                    case 0b00010101:
                        fen_output += "B";
                        break;
                    case 0b00010110:
                        fen_output += "K";
                        break;
                    case 0b00010111:
                        fen_output += "K";
                        break;
                    case 0b00011000:
                        fen_output += "Q";
                        break;
                    case 0b00011001:
                        fen_output += "R";
                        if (i == 1)
                            castle_W_Q = true;
                        else if (i == 8)
                            castle_W_K = true;
                        break;
                    case 0b00011010:
                        fen_output += "R";
                        break;
                }
            }
            if (square_count != 0)
                fen_output += Convert.ToString(square_count);
            square_count = 0;
            if (j != 1)
                fen_output += "/";
        }

        fen_output += color == 0 ? " b " : " w ";

        if (castle_W_K)
            fen_output += "K";
        if (castle_W_Q)
            fen_output += "Q";
        if (castle_B_K)
            fen_output += "k";
        if (castle_B_Q)
            fen_output += "q";
        if (!castle_B_K && !castle_B_Q && !castle_W_K && !castle_W_Q)
            fen_output += "- ";

        if (en_passent_x != 0)
            fen_output += " " + Convert.ToString(Letters[en_passent_x]) + Convert.ToString(Numbers[en_passent_y]) + " ";
        else
            fen_output += " - ";

        fen_output += fifty_move_rule + " 0";

        return fen_output;
    }
}
class TTableEntry
{
    public int[] BestMove;
    public float Score;
    public byte depth;
    public bool fail_high = false , fail_low = false;
    public TTableEntry(int[] Bestmove, float CurrentScore, byte Currentdepth, bool cut_node, bool all_node)
    {
        BestMove = Bestmove;
        Score = CurrentScore;
        depth = Currentdepth;
        fail_high = cut_node;
        fail_low = all_node;
    }
}
class RepetitionTable
{
    public long Key = 0;
    public RepetitionTable(long key)
    {
        Key = key;
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

