using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.IO;
class AlphaBeta
{
    Stopwatch sw = new Stopwatch();
    Semaphore time_acces = new Semaphore(1, 1), depth_acces = new Semaphore(1, 1);
   // StreamWriter stream_writer;
    bool stop = false;
    int[] sorting_counter = new int[300];
    int mate_value = 60000;
    int illegal_position_value = 80000;
    int max_depth = 256;

    public long[] repetitions = new long[307];
    bool[] repetion_lookup = new bool[ushort.MaxValue];

    standart stuff = new standart();
    standart_chess chess_stuff = new standart_chess();
    public NNUE_avx2 ValueNet;
    public MoveGen MoveGenerator = new MoveGen();
    public Classic_Eval eval = new Classic_Eval();
    public Move_Ordering_Heuristics move_order = new Move_Ordering_Heuristics();
    Random random = new Random(59675943);
    int Nodecount = 0, max_ply = 0 , move_counter = 0 , root_depth = 1;
    int[,,] move_reductions = new int[2, 64, 64];
    bool[] null_move_pruning = new bool[byte.MaxValue + 1];

    long[][,] piece_hashes = new long[27][,];
    int[] node_values = new int[byte.MaxValue + 1];
    public long black_to_move, time_to_use = 0;
    public byte[,] HashTable = new byte[0, 0];

    public AlphaBeta(int HashSize)
    {
        HashTable = new byte[HashSize * 55556 , 18];
        HashFunctionInit();
        init_reductions();
        ValueNet = new NNUE_avx2(true);
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
        long time_to_search = time_to_use;
        int search_depth = 1;
        while(stopwatch.ElapsedMilliseconds <= time_to_search || search_depth == 1)
        {
            Thread.Sleep(1);
            depth_acces.WaitOne();
            search_depth = root_depth;
            depth_acces.Release();

            time_acces.WaitOne();
            time_to_search = time_to_use;
            time_acces.Release();
        }
        stop = true;
    }
    public int[] iterative_deepening(byte[,] board, byte color, int depth, bool NNUE_avx2)
    {
        //initialize the variables
        move_order.reset_movesort();
        stop = false;
        int[] EnPassent = new int[2];
        byte othercolor = (byte)(1 - color);
        Array.Copy(MoveGenerator.EnPassent, EnPassent, EnPassent.Length);
        List<int[]> last_best_moves = new List<int[]>();
        bool search_pv = true, in_check = MoveGenerator.CompleteCheck(board, othercolor), gives_check = false;
        int[] Output = new int[0], MoveUndo;
        int movecount = 0 , fifty_move_rule = MoveGenerator.fifty_move_rule;
        List<int[]> moves = MoveGenerator.ReturnPossibleMoves(board, color);
        pv_out current_variation = new pv_out(), pv = new pv_out();
        int alpha = -illegal_position_value, delta_a = 0, delta_b = 0, window_a = 0, window_b = 0, last_best = 0, last_last_best = 0, current_score = 0;
        bool check = MoveGenerator.CompleteCheck(board, othercolor);
        Accumulator currentacc = new Accumulator(128);
        move_and_eval_list move_list = new move_and_eval_list();
        time_acces.WaitOne();
        long theoretical_time_usage = time_to_use;
        time_acces.Release();
        //get the key for the position
        long key = zobrist_hash(board, color);

        //start the stopwatch
        sw.Start();
        sw.Start();

        if (NNUE_avx2)
        {
            //the the accumulator position to the starting position
            ValueNet.set_acc_from_position(board);

            //copy the accumulator for the current position
            currentacc = chess_stuff.acc_copy(ValueNet.acc);
            current_score = chess_stuff.convert_wdl_to_millipawn(ValueNet.AccToOutput(ValueNet.acc, color));
        }
        else
            current_score = eval.pesto_eval(board, color);

        node_values[0] = !in_check ? current_score : illegal_position_value;

        for (int current_depth = 1; current_depth <= depth; current_depth++)
        {
            depth_acces.WaitOne();
            root_depth = current_depth;
            depth_acces.Release();

            if (current_depth >= 3 && Math.Abs(last_last_best) < mate_value) 
            {
                //if the current depth is larger then 2 reajust the window
                delta_a = -125;
                window_a = last_last_best + delta_a;
                delta_b = 125;
                window_b = last_last_best + delta_b;
                alpha = window_a;
            }
            else
            {
                window_a = -illegal_position_value;
                window_b = illegal_position_value;
                alpha = window_a;
            }

            while (!stop)
            {
                movecount = 0;
                search_pv = true;

                move_list = move_order.evaluate_moves(board, stuff.copy_int_array_list(moves), 0, color, false, IsvalidEntry(key) == 1 ? GetInfoFromEntry(key).BestMove : new int[0]);

                while (move_list.eval_list.Count > 0)
                {
                    movepick current_move = move_order.pick_next_move(move_list);

                    //Debug.Assert(!stuff.int_array_equal(current_move.move, new int[] { 4, 5, 4, 4 }) || current_depth != 3);

                    long new_key = zobrist_hash_update(key, board, color, current_move.move);
                    movecount++;

                    current_variation = new pv_out();
                    move_order.add_current_move(current_move.move, board, 0);

                    //play the move
                    board = make_move(board, color, current_move.move, NNUE_avx2);

                    //copy the unmake move into move undo
                    MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
                    Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);

                    //calculate if the current move gives check
                    gives_check = MoveGenerator.CompleteCheck(board, color);

                    //find if the current position is a terminal position
                    //determining the mate value 2 => not a terminal position , 0 => draw , 1 => mate for white , -1 => mate for black
                    int matingValue = MoveGenerator.Mate(board, othercolor);

                    //checking if the position is not a terminal node
                    if (matingValue != 2)
                    {
                        //if the position is a terminal node the value for the node is set to the mating value from the perspective of the current color
                        current_variation.value = matingValue == 0 ? 0 : mate_value + max_depth;
                        current_variation.principalVariation.Insert(0, current_move.move);
                    }
                    else if (!MoveGenerator.CompleteCheck(board, othercolor))
                    {
                        //if the current depth is 1 perform a quiescent search
                        if (current_depth <= 1)
                        {
                            current_variation.value = -quiescence_search(board, -window_b, -alpha, othercolor, NNUE_avx2, 0, 1, new_key);

                            current_variation.principalVariation.Add(current_move.move);
                        }
                        //else perform a normal pv search
                        else
                        {
                            //perform a pv search
                            if (search_pv)
                            {
                                current_variation = principal_variation_search(board, othercolor, current_depth - 1, 1, -window_b, -alpha, gives_check, NNUE_avx2, new_key);
                                current_variation.value = -current_variation.value;
                                current_variation.principalVariation.Insert(0, current_move.move);
                            }
                            else
                            {
                                current_variation.value = -zero_window_search(board, othercolor, current_depth - 1, 1, -(alpha + 1), -alpha, gives_check, NNUE_avx2, new_key);

                                if (stop)
                                {
                                    //undo the move
                                    board = unmake_move(board, MoveUndo, currentacc, fifty_move_rule, NNUE_avx2);
                                    break;
                                }

                                if (current_variation.value > alpha)
                                {
                                    current_variation = principal_variation_search(board, othercolor, current_depth - 1, 1, -window_b, -alpha, gives_check, NNUE_avx2, new_key);
                                    current_variation.value = -current_variation.value;
                                    current_variation.principalVariation.Insert(0, current_move.move);
                                }
                            }
                        }
                    }

                    //undo the move
                    board = unmake_move(board, MoveUndo, currentacc, fifty_move_rule, NNUE_avx2);

                    //determine if the current move is better than the currently best move only if it is 
                    if (alpha < current_variation.value && current_variation.value != illegal_position_value)
                    {
                        if (chess_stuff.is_capture(current_move.move, board))
                            move_order.update_history_move(board, current_move.move, new int[0], new int[0], Math.Min((float)(depth * depth) / 10f, 40), color, 0);
                        else
                            move_order.update_chistory_move(board, current_move.move, color, Math.Min((float)(depth * depth) / 10f, 40));

                        alpha = current_variation.value;
                        pv = current_variation;
                        search_pv = false;
                    }

                    if (stop || alpha >= window_b)
                        break;
                }

                if (alpha <= window_a)
                {
                    delta_a *= 2;
                    window_a = last_last_best + delta_a;
                    alpha = window_a;
                }
                else if(alpha >= window_b)
                {
                    delta_b *= 2;
                    alpha = window_a;
                    window_b = last_last_best + delta_b;
                }
                else
                    break;
            }
            if (!stop)
            {
                Output = pv.principalVariation[0];

                //adjust timing
                time_acces.WaitOne();
                if (current_depth > 2)
                {
                    //if the timing is already maximal do not change it
                    //else if th bestmove is different to the last best move make the time usage larger
                    if (theoretical_time_usage * 14 > time_to_use * 10 && !stuff.int_array_equal(Output, GetInfoFromEntry(key).BestMove))
                        time_to_use += theoretical_time_usage / 10;
                    //else if it is really low do not change it
                    //else make the time usage smaller
                    else if (theoretical_time_usage * 4 < time_to_use * 10)
                        time_to_use -= theoretical_time_usage / 10;
                }
                time_acces.Release();

                //add the best move to the hash table
                AddToTable(Output, current_depth, alpha, key, 0, 0);
            }
            //after a finished search return the main informations 
            if (!stop)
            {
                if (Math.Abs(alpha) < mate_value) 
                    Console.WriteLine("info depth {2} seldepth {3} nodes {1} nps {4} time {5} score cp {0} pv {6}", alpha / 10, Nodecount, current_depth, current_depth + max_ply, (int)(((float)(Nodecount) * 1000) / (sw.ElapsedMilliseconds > 0 ? (float)sw.ElapsedMilliseconds : 1)), (int)(sw.ElapsedMilliseconds), variation_to_string(pv.principalVariation));
                else
                    Console.WriteLine("info depth {2} seldepth {3} nodes {1} nps {4} time {5} score mate {0} pv {6}", -(alpha - (alpha / Math.Abs(alpha)) * (max_depth + mate_value) - 1) / 2, Nodecount, current_depth, current_depth + max_ply, (int)(((float)(Nodecount) * 1000) / (sw.ElapsedMilliseconds > 0 ? (float)sw.ElapsedMilliseconds : 1)), (int)(sw.ElapsedMilliseconds), variation_to_string(pv.principalVariation));
            }
             
            //reset various variables
            last_last_best = last_best;
            last_best = alpha;
            max_ply = 0;

            if (stop)
            {
                stop = false;
                break;
            }
        }

        //reset the nodecount
        Nodecount = 0;

        //stop the stopwatch
        sw.Stop();
        sw.Reset();

        //return the best move
        return Output;
    }
    public pv_out principal_variation_search(byte[,] board, byte color, int depth, int ply, int alpha, int beta, bool in_check, bool NNUE_avx2, long key)
    {
        //define the variables
        byte othercolor = (byte)(1 - color);
        bool found_legal_position = false, search_pv = true, gives_check = false, two_fold_repetition = false, is_futile = false , improving = true , full_depth_search = false , fail_low = false , pruning_is_safe = false;
        int[] MoveUndo, BestMove = new int[0];
        int movecount = 0 , fifty_move_rule = MoveGenerator.fifty_move_rule , interesting_move_count , new_depth;
        int current_score = 0, decrease = 0, improvement = 0;
        Accumulator currentacc = new Accumulator(128);
        pv_out Output = new pv_out(), current_variation = new pv_out();
        Output.value = alpha;
        TTableEntry entry = new TTableEntry(new int[0], 0, 0 , false , false);
        move_and_eval_list move_list = new move_and_eval_list();
        List<int[]> played_moves = new List<int[]>();

        if (fifty_move_rule == 100 || stop)
        {
            Output.value = 0;
            return Output;
        }

        //threefold repetition
        if(is_in_fast_lookup(key))
        {
            if (repetition_count(key) == 2)
            {
                Output.value = 0;
                return Output;
            }
            else if (repetition_count(key) == 1)
                two_fold_repetition = true;
        }

        int KeyValid = IsvalidEntry(key);

        if (KeyValid > -2)
        {
            entry = GetInfoFromEntry(key);
            entry.Score -= Math.Abs(entry.Score) >= mate_value ? ply : 0;

            if (KeyValid == 1)
            {
                //if the position has the right depth return the value of the position
                if (entry.depth >= depth)
                {
                    if (entry.Score >= beta && !entry.fail_low)
                    {
                        Output.value = beta;
                        return Output;
                    }
                    if (entry.Score <= alpha && !entry.fail_high)
                    {
                        Output.value = alpha;
                        return Output;
                    }
                }
            }
        }

        List<int[]> moves = MoveGenerator.ReturnPossibleMoves(board, color);

        //copy the accumulator for the current position
        currentacc = chess_stuff.acc_copy(ValueNet.acc);

        //the position is illegal
        if(moves == null)
        {
            Output.value = -illegal_position_value;
            return Output;
        }

        //internal iterative deepening
        if (depth >= 1000 && KeyValid != 1) 
        {
            int current_value = 0, best_value = -illegal_position_value;

            //sort the moves
            move_list = move_order.evaluate_moves(board, stuff.copy_int_array_list(moves), ply, color, false, BestMove);
            interesting_move_count = move_order.tactical_move_counter;

            while (move_list.eval_list.Count > 0)
            {
                movepick current_move = move_order.pick_next_move(move_list);
                long new_key = zobrist_hash_update(key, board, color, current_move.move);

                //play the move
                board = make_move(board, color, current_move.move, NNUE_avx2);

                MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
                Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);

                //calculate if the current move gives check
                gives_check = MoveGenerator.CompleteCheck(board, color);

                current_value = -principal_variation_search(board, othercolor, depth / 2, ply + 1, -beta, -best_value, gives_check, NNUE_avx2, new_key).value;

                if (current_value > best_value && current_value != illegal_position_value)
                {
                    best_value = current_value;
                    BestMove = new int[current_move.move.Length];
                    Array.Copy(current_move.move, BestMove, current_move.move.Length);
                }

                //undo the move
                board = unmake_move(board, MoveUndo, currentacc, fifty_move_rule, NNUE_avx2);

                if (best_value >= beta)
                    break;
            }
        }

        AddPositionToLookups(key);

        //get the current score
        if (!in_check)
        {
            if (NNUE_avx2)
                current_score = chess_stuff.convert_wdl_to_millipawn(ValueNet.AccToOutput(ValueNet.acc, color));
            else
                current_score = eval.pesto_eval(board, color);
        }

        /* update the value in the value array
         * if we are in check do not update the value*/

        node_values[ply] = !in_check ? current_score : illegal_position_value;

        //set the improving flag high if the current value is an improvement
        improvement = ply < 2 ? 0 : node_values[ply] - node_values[ply - 2];
        improving = (improvement > 0 || ply < 2) && !in_check;

        /*we should be able to prune branches only in specific cases
         * when we are not in check
         * and when the depth of the root is larger then 3
         */

        //sort the moves
        move_list = move_order.evaluate_moves(board, moves, ply, color, false, KeyValid == 1 ? entry.BestMove : BestMove);
        interesting_move_count = move_order.tactical_move_counter;

        pruning_is_safe = !in_check && root_depth > 3 && ply > 2;

        while (move_list.movelist.Count > 0)
        {
            movepick current_move = move_order.pick_next_move(move_list);
            long new_key = zobrist_hash_update(key, board, color, current_move.move);
            movecount++;

            current_variation = new pv_out();
            move_order.add_current_move(current_move.move, board, ply);

            //play the move
            board = make_move(board, color, current_move.move, NNUE_avx2);

            MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
            Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);

            //calculate if the current move gives check
            gives_check = MoveGenerator.CompleteCheck(board, color);

            //find futile moves
            if (pruning_is_safe && alpha > -mate_value && !is_futile && depth <= 7 && MoveGenerator.non_pawn_material) 
            {
                if (movecount >= move_pruning(depth, improving))
                    is_futile = true;
                else if (current_variation.value + extended_futility_pruning_margin(depth , true) <= alpha && Math.Abs(alpha) < mate_value)
                    is_futile = true;
            }

            //set the new depth
            new_depth = depth - 1;

            /*calculate depth extentions*/

            //check extention
            if (in_check)
                new_depth++;

            //if the current depth is 1 do a quiescent search
            if (new_depth <= 0)
            {
                current_variation.value = -quiescence_search(board, -beta, -alpha, othercolor, NNUE_avx2, 0 , ply + 1, new_key);
                current_variation.principalVariation.Add(current_move.move);
            }
            //else just call the function recursively
            else
            {
                if (search_pv)
                {
                    current_variation = principal_variation_search(board, othercolor, new_depth, ply + 1, -beta, -alpha, gives_check, NNUE_avx2 , new_key);
                    current_variation.value = -current_variation.value;
                    current_variation.principalVariation.Insert(0,current_move.move);
                }
                else
                {
                    //late move reduction
                    if (depth > 2 && movecount > 1 && !gives_check)
                    {
                        if (movecount > interesting_move_count)
                        {
                            decrease = reduction(depth, movecount, true);

                            //if we are improving or not decrease
                            decrease -= Math.Max(Math.Min(improvement / 500, 2), -2);

                            //if the king is in check and the king moves
                            if (in_check && (board[current_move.move[2], current_move.move[3]] & 0b00001111) >> 1 == 0b11)
                                decrease -= 1;

                            //at least a counter move
                            if (current_move.eval >= 4000)
                                decrease -= 1;

                            //normal quiet move
                            else
                                decrease += (int)Math.Max(Math.Min(current_move.eval / 100, 2), -2);
                        }

                        int lmr_depth = Math.Max(Math.Min(new_depth, new_depth - decrease), 1);

                        current_variation.value = -zero_window_search(board, othercolor, lmr_depth, ply + 1, -(alpha + 1), -alpha, gives_check, NNUE_avx2 ,new_key);

                        if (current_variation.value == illegal_position_value)
                        {
                            //undo the move
                            board = unmake_move(board, MoveUndo, currentacc, fifty_move_rule, NNUE_avx2);
                            movecount--;
                            continue;
                        }

                        if (current_variation.value > alpha && lmr_depth < new_depth)
                            full_depth_search = true;
                    }
                    else
                        full_depth_search = true;

                    if(full_depth_search)
                    {
                        current_variation.value = -zero_window_search(board, othercolor, new_depth, ply + 1, -(alpha + 1), -alpha, gives_check, NNUE_avx2, new_key);

                        full_depth_search = false;

                        if (current_variation.value == illegal_position_value)
                        {
                            //undo the move
                            board = unmake_move(board, MoveUndo, currentacc, fifty_move_rule, NNUE_avx2);
                            movecount--;
                            continue;
                        }
                    }

                    if (current_variation.value > alpha && current_variation.value < beta)
                    {
                        current_variation = principal_variation_search(board, othercolor, new_depth, ply + 1, -beta, -alpha, gives_check, NNUE_avx2, new_key);
                        current_variation.value = -current_variation.value;
                        current_variation.principalVariation.Insert(0, current_move.move);
                    }
                }
            }

            //undo the move
            board = unmake_move(board, MoveUndo, currentacc, fifty_move_rule, NNUE_avx2);

            if (current_variation.value != illegal_position_value)
            {
                played_moves.Add(current_move.move);
                found_legal_position = true;
                if (current_variation.value > alpha)
                {
                    if (movecount > interesting_move_count)
                        move_order.update_history_move(board, current_move.move, move_order.current_move[ply - 1], ply - 2 >= 0 ? move_order.current_move[ply - 2] : new int[0], Math.Min((float)(depth * depth) / 10f, 40), color, ply);
                    else
                        move_order.update_chistory_move(board, current_move.move, color, Math.Min((float)(depth * depth) / 10f, 40));

                    alpha = current_variation.value;
                    BestMove = new int[current_move.move.Length];
                    Array.Copy(current_move.move, BestMove, current_move.move.Length);
                    Output = current_variation;
                    search_pv = false;
                }

                //if the branch is not better then the currently best branch we can prune the other positions
                if (current_variation.value >= beta)
                {
                    //store the killer move history moves and counter moves
                    move_order.update_histories(board, current_move.move, played_moves, null_move_pruning, color, depth, ply);

                    //add the best move to the hash table if the current depth is greater than the depth of the entry or thre is no entry in the hash table
                    if (!stop && (KeyValid == -2 || entry.depth <= depth))
                        AddToTable(current_move.move, depth, beta, key, 1, 0);

                    RemovePositionFromLookups(key, !two_fold_repetition);

                    Output.principalVariation = new List<int[]>();
                    Output.value = beta;
                    return Output;
                }
            }

            if (movecount >= interesting_move_count && current_move.eval < 4000 && is_futile && false)
                break;
        }

        RemovePositionFromLookups(key, !two_fold_repetition);

        //if no move was legal return the score for mate
        if (!found_legal_position)
        {
            //mate
            if (in_check)
            {
                Output.value = -(mate_value + max_depth - ply);
                AddToTable(new int[0], max_depth, Output.value, key, 0, 0);
                Output.principalVariation = new List<int[]>();
                return Output;
            }
            //stalemate
            else
            {
                Output.value = 0;
                AddToTable(new int[0], max_depth, Output.value, key, 0, 0);
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
                BestMove = played_moves[0];
            }

            //add the best move to the hash table if the current depth is greater than the depth of the entry or thre is no entry in the hash table
            if (!stop && (KeyValid == -2 || entry.depth <= depth))
                AddToTable(BestMove, depth, Math.Abs(alpha) > mate_value ? alpha + ply : alpha, key, 0, (byte)(fail_low ? 1 : 0));

            //return the best score
            return Output;
        }
    }
    public int zero_window_search(byte[,] board, byte color, int depth, int ply, int alpha, int beta, bool in_check, bool NNUE_avx2,long key)
    {
        //define the variables
        int current_score = 0 , decrease = 0, improvement = 0;
        byte othercolor = (byte)(1 - color);
        bool found_legal_position = false, gives_check = false, two_fold_repetition = false , full_depth_search = false;
        int movecount = 0 , fifty_move_rule = MoveGenerator.fifty_move_rule , interesting_move_count , new_depth = 0;
        int[] MoveUndo, BestMove = new int[0];
        bool is_futile = false , improving = false , pruning_is_safe = false;
        Accumulator currentacc = new Accumulator(128);
        TTableEntry entry = new TTableEntry(new int[0], 0, 0, false, false);
        move_and_eval_list move_list = new move_and_eval_list();
        List<int[]> played_moves = new List<int[]>();

        if (fifty_move_rule == 100 || stop)
            return 0;

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
            entry.Score -= Math.Abs(entry.Score) >= mate_value ? ply : 0;
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

        List<int[]> moves = MoveGenerator.ReturnPossibleMoves(board, color);

        //the position is illegal
        if(moves == null)
            return -illegal_position_value;

        //copy the accumulator for the current position
        currentacc = chess_stuff.acc_copy(ValueNet.acc);

        if (!in_check)
        {
            if (NNUE_avx2)
                current_score = chess_stuff.convert_wdl_to_millipawn(ValueNet.AccToOutput(ValueNet.acc, color));
            else
                current_score = eval.pesto_eval(board, color);
        }

        /* update the value in the value array
         * if we are in check do not update the value*/

        node_values[ply] = !in_check ? current_score : illegal_position_value;

        //set the improving flag high if the current value is an improvement
        improvement = ply < 2 ? 0 : node_values[ply] - node_values[ply - 2];
        improving = (improvement > 0 || ply < 2) && !in_check;

        /*we should be able to prune branches only in specific cases
         * when we are not in check
         * and when the depth of the root is larger then 3
         */

        pruning_is_safe = !in_check && root_depth > 3;

        if (pruning_is_safe) 
        {
            /*Razoring
             * 
             * if the current score is really bad,
             * we try a quiescence search to look if a tactical sequence can make up for the bad score
             * if this is not the case we just prune 
             */

            if (depth <= 5 && current_score < alpha - razoring_margin(depth) && alpha < mate_value && false)
            {
                int test_value = quiescence_search(board, alpha - 1, alpha, color, NNUE_avx2, 0, ply + 1, key);

                if (test_value < alpha)
                    return test_value;
            }

            //Reverse Futility Pruning
            if (depth <= 7 && current_score - reverse_futility_pruning_margin(depth, improving) >= beta && false)
                return beta;

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

            if (depth >= 3 &&
                current_score >= beta &&
                MoveGenerator.non_pawn_material &&
                !null_move_pruning[ply - 1] &&
                (KeyValid != 1 || !entry.fail_low && entry.Score >= beta))
            {
                int nmp_score = 0;

                /* calculate the depth for the null move search
                 * 
                 * 1) the base depth reduction is 2.5
                 * 
                 * 2) else the depth gets reduced by a factor 1/10
                 * 
                 * 3) the larger the delta between the standing pat and beta the more we can reduce
                 */

                int null_move_search_depth = depth - 1 - (3 + depth / 6 + Math.Min(3, (int)(current_score - beta) / 6000));

                //Make null Move

                if (null_move_search_depth <= 0)
                    nmp_score = -quiescence_search(board, -beta, -alpha, othercolor, NNUE_avx2, 0, ply + 1, key ^ black_to_move);
                else
                {
                    //add the null move search to the table
                    null_move_pruning[ply] = true;

                    nmp_score = -zero_window_search(board, othercolor, null_move_search_depth, ply + 1, -beta, -alpha, MoveGenerator.CompleteCheck(board, color), NNUE_avx2, key ^ black_to_move);

                    //remove the null move search from the table
                    null_move_pruning[ply] = false;
                }

                //Unmake the null move


                /*if the new score is better
                 *and the next position is not illegal
                 *and the next value is not mate
                 *return beta
                 */
                if (nmp_score >= beta && nmp_score != illegal_position_value && Math.Abs(beta) < mate_value)
                    return beta;
            }
        }

        //sort the moves
        move_list = move_order.evaluate_moves(board, moves, ply, color, false, entry.BestMove);
        interesting_move_count = move_order.tactical_move_counter;

        AddPositionToLookups(key);

        while (move_list.eval_list.Count > 0)
        {
            movepick current_move = move_order.pick_next_move(move_list);
            movecount++;
            move_order.add_current_move(current_move.move, board, ply);
            long new_key = zobrist_hash_update(key, board, color, current_move.move);

            //play the move
            board = make_move(board, color, current_move.move, NNUE_avx2);

            MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
            Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);

            //calculate if the current move gives check
            gives_check = MoveGenerator.CompleteCheck(board, color);

            //find futile moves
            if (pruning_is_safe && Math.Abs(alpha) < mate_value && !is_futile && depth <= 7 && MoveGenerator.non_pawn_material)
            {
                if (movecount >= move_pruning(depth, improving))
                    is_futile = true;
                else if (current_score + extended_futility_pruning_margin(depth , false) <= alpha)
                    is_futile = true;
            }

            //set the new depth
            new_depth = depth - 1;

            /*calculate depth depth extentions*/

            //check extention
            if (in_check)
                new_depth++;

            //if the current depth is 1 do a quiescent search
            if (new_depth <= 0)
                current_score = -quiescence_search(board, -beta, -alpha, othercolor, NNUE_avx2, 0 , ply + 1,new_key);

            //else just call the function recursively
            else
            {
                //late move reduction
                if (depth > 2 && movecount > 1 && !gives_check) 
                {
                    if (movecount > interesting_move_count)
                    {
                        decrease = reduction(depth, movecount, false);

                        //if we are improving or not decrease
                        decrease -= Math.Max(Math.Min(improvement / 500, 2), -2);

                        //if the king is in check and the king moves
                        if (in_check && (board[current_move.move[2], current_move.move[3]] & 0b00001111) >> 1 == 0b11)
                            decrease -= 1;

                        //at least a counter move
                        if (current_move.eval >= 4000)
                            decrease -= 1;

                        //normal quiet move
                        else
                            decrease += (int)Math.Max(Math.Min(current_move.eval / 100, 3), -3);
                    }

                    int lmr_depth = Math.Max(Math.Min(new_depth, new_depth - decrease), 1);

                    current_score = -zero_window_search(board, othercolor, lmr_depth, ply + 1, -beta, -alpha, gives_check, NNUE_avx2,new_key);

                    if (current_score == illegal_position_value)
                    {
                        //undo the move
                        board = unmake_move(board, MoveUndo, currentacc, fifty_move_rule, NNUE_avx2);
                        movecount--;
                        continue;
                    }

                    if (current_score > alpha && lmr_depth < new_depth)
                        full_depth_search = true;
                }
                else
                    full_depth_search = true;

                if(full_depth_search)
                {
                    current_score = -zero_window_search(board, othercolor, new_depth, ply + 1, -beta, -alpha, gives_check, NNUE_avx2,new_key);

                    full_depth_search = false;

                    if (current_score == illegal_position_value)
                    {
                        //undo the move
                        board = unmake_move(board, MoveUndo, currentacc, fifty_move_rule, NNUE_avx2);
                        movecount--;
                        continue;
                    }
                }
            }

            //undo the move
            board = unmake_move(board, MoveUndo, currentacc, fifty_move_rule, NNUE_avx2);

            if (current_score != illegal_position_value)
            {
                played_moves.Add(current_move.move);
                found_legal_position = true;

                //if the branch is not better then the currently best branch we can prune the other positions
                if (current_score >= beta)
                {
                    //store the killer move history moves and counter moves
                    move_order.update_histories(board, current_move.move, played_moves, null_move_pruning, color, depth, ply);

                    //add the best move to the hash table if the current depth is greater than the depth of the entry or thre is no entry in the hash table
                    if (!stop && (KeyValid == -2 || entry.depth <= depth))
                        AddToTable(current_move.move, depth, beta, key, 1, 0);

                    RemovePositionFromLookups(key, !two_fold_repetition);

                    return beta;
                }
            }
            else
                movecount--;

            if (movecount >= interesting_move_count && current_move.eval < 4000 && is_futile && false)
                break;
        }

        RemovePositionFromLookups(key, !two_fold_repetition);

        //if no move was legal return the score for a terminal node
        if (!found_legal_position)
        {
            //mate
            if (in_check)
            {
                AddToTable(new int[0], max_depth, -(mate_value + max_depth - ply), key, 0, 0);
                return -(mate_value + max_depth - ply);
            }
            //stalemate
            else
            {
                AddToTable(new int[0], max_depth, 0, key, 0, 0);
                return 0;
            }
        }
        else
        {
            //add the best move to the hash table if there is no entry in the hash table
            if (!stop && (KeyValid == -2 || entry.depth <= depth))
                AddToTable(played_moves[0], depth, Math.Abs(alpha) > mate_value ? alpha + ply : alpha, key, 0, 1);

            return alpha;
        }
    }
    public int quiescence_search(byte[,] board, int alpha, int beta, byte color, bool NNUE_avx2, int depth , int ply, long key)
    {
        if (stop)
            return 0;

        //look for repetitions
        if (depth == 0)
        {
            //threefold repetition
            if (is_in_fast_lookup(key) && repetition_count(key) == 2 || MoveGenerator.fifty_move_rule == 100)
                return 0;
        }

        //define the variables
        Nodecount++;
        int standing_pat = -illegal_position_value, current_score = 0;
        byte othercolor = (byte)(1 - color);
        bool in_check = MoveGenerator.CompleteCheck(board, othercolor) , legal_move = false, fail_low = true;
        int[] MoveUndo, BestMove = new int[0];
        int move_count = 0;
        List<int[]> moves;
        Accumulator currentacc = new Accumulator(128);
        move_and_eval_list move_list = new move_and_eval_list();
        TTableEntry entry = new TTableEntry(new int[0], 0, 0, false, false);
        int KeyValid = IsvalidEntry(key);

        if (KeyValid > -2)
        {
            entry = GetInfoFromEntry(key);
            entry.Score -= Math.Abs(entry.Score) >= mate_value ? ply : 0;

            if (KeyValid == 1)
            {
                //if the score is larger or equal to beta we can return beta
                if (entry.Score >= beta && !entry.fail_low)
                    return beta;
                //else if the score is certain and it is smaller then alpha we have an alpha cutoff
                if (entry.Score <= alpha && !entry.fail_high)
                    return alpha;
            }
        }

        //copy the accumulator for the current position
        currentacc = chess_stuff.acc_copy(ValueNet.acc);

        //if we are in check look for  other moves
        if (in_check)
            moves = MoveGenerator.ReturnPossibleMoves(board, color);
        //else just look for captures
        else
            moves = MoveGenerator.ReturnPossibleCaptures(board, color);

        //if the position is legal
        if (moves != null)
        {
            //if we are in check standing pat is no allowed because we search all moves and not only captures
            if (!in_check)
            {
                if (NNUE_avx2)
                    standing_pat = chess_stuff.convert_wdl_to_millipawn(ValueNet.AccToOutput(ValueNet.acc, color));
                else
                    standing_pat = eval.pesto_eval(board, color);
            }

            //if the branch is not better then the currently best branch we can prune the other positions
            if (standing_pat >= beta)
                return beta;

            //delta pruning
            if (standing_pat < alpha - 9000 && !in_check)
                return alpha;

            if (standing_pat > alpha)
                alpha = standing_pat;

            //if the position is quiet return the evaluation
            if (moves.Count == 0)
            {
                max_ply = Math.Max(ply, max_ply);

                return alpha;
            }

            //sort the moves
            move_list = move_order.evaluate_moves(board, moves, ply, color, true, entry.BestMove);
        }

        //the position is illegal
        else return -illegal_position_value;

        while (move_list.movelist.Count > 0)
        {
            movepick current_move = move_order.pick_next_move(move_list);
            long new_key = zobrist_hash_update(key, board, color, current_move.move);
            move_count++;

            //play the move
            board = make_move(board, color, current_move.move, NNUE_avx2);

            MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
            Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);

            //calls itself recursively
            current_score = -quiescence_search(board, -beta, -alpha, othercolor, NNUE_avx2, depth + 1, ply + 1, new_key);

            //undo the move
            board = unmake_move(board, MoveUndo, currentacc, 0, NNUE_avx2);

            //if the current score is not 2 the position is not illegal and therefore we have found a legal move
            if (current_score != illegal_position_value)
            {
                if (move_count == 0) BestMove = stuff.int_array_copy(current_move.move);
                legal_move = true;

                if (current_score > alpha)
                {
                    fail_low = false;
                    alpha = current_score;
                    BestMove = stuff.int_array_copy(current_move.move);

                    //if the branch is not better then the currently best branch we can prune the other positions
                    if (current_score >= beta)
                    {
                        if (!stop && (KeyValid == -2 || entry.depth == 0))
                            AddToTable(current_move.move, 0, beta, key, 1, 0);
                        return beta;
                    }
                }
            }
            else
                move_count--;
        }

        //add the best move to the hash table if the current depth is greater than the depth of the entry or thre is no entry in the hash table
        if (!stop && (KeyValid == -2 || entry.depth <= depth))
            AddToTable(BestMove, 0, alpha, key, 0, (byte)(fail_low ? 1 : 0));

        //return the best score
        return alpha;
    }
    public alpha_beta_output selfplay_iterative_deepening(byte[,] board, byte color, int depth, bool NNUE_avx2)
    {
        //initialize the variables
        move_order.reset_movesort();
        stop = false;
        int[] EnPassent = new int[2];
        byte othercolor = (byte)(1 - color);
        Array.Copy(MoveGenerator.EnPassent, EnPassent, EnPassent.Length);
        List<int[]> last_best_moves = new List<int[]>();
        bool search_pv = true, in_check = MoveGenerator.CompleteCheck(board, othercolor), gives_check = false;
        alpha_beta_output Output = new alpha_beta_output();
        int[] MoveUndo;
        int movecount = 0, fifty_move_rule = MoveGenerator.fifty_move_rule;
        List<int[]> moves = MoveGenerator.ReturnPossibleMoves(board, color);
        pv_out current_variation = new pv_out(), pv = new pv_out();
        int alpha = -illegal_position_value, delta_a = 0, delta_b = 0, window_a = 0, window_b = 0, last_best = 0, last_last_best = 0, current_score = 0;
        bool check = MoveGenerator.CompleteCheck(board, othercolor);
        Accumulator currentacc = new Accumulator(128);
        move_and_eval_list move_list = new move_and_eval_list();
        bool position_is_quiet = true;

        //get the key for the position
        long key = zobrist_hash(board, color);

        //start the stopwatch
        sw.Start();
        sw.Start();

        //threefold repetition
        if (is_in_fast_lookup(key))
        {
            if (repetition_count(key) == 2)
            {
                Output.draw = true;
                return Output;
            }
        }

        if (fifty_move_rule == 100)
        {
            Output.draw = true;
            return Output;
        }

        if (NNUE_avx2)
        {
            //the the accumulator position to the starting position
            ValueNet.set_acc_from_position(board);

            //copy the accumulator for the current position
            currentacc = chess_stuff.acc_copy(ValueNet.acc);
        }

        if (NNUE_avx2)
            current_score = chess_stuff.convert_wdl_to_millipawn(ValueNet.AccToOutput(ValueNet.acc, color));
        else
            current_score = eval.pesto_eval(board, color);

        node_values[0] = !in_check ? current_score : illegal_position_value;

        for (int current_depth = 1; current_depth <= depth; current_depth++)
        {
            root_depth = current_depth;

            if (current_depth >= 3 && Math.Abs(last_last_best) < mate_value)
            {
                //if the current depth is larger then 4 reajust the window
                delta_a = -125;
                window_a = last_last_best + delta_a;
                delta_b = 125;
                window_b = last_last_best + delta_b;
                alpha = window_a;
            }
            else
            {
                window_a = -illegal_position_value;
                window_b = illegal_position_value;
                alpha = window_a;
            }

            while (!stop)
            {
                movecount = 0;
                search_pv = true;

                move_list = move_order.evaluate_moves(board, moves, 0, color, false, IsvalidEntry(key) == 1 ? GetInfoFromEntry(key).BestMove : new int[0]);

                while (move_list.eval_list.Count > 0)
                {
                    movepick current_move = move_order.pick_next_move(move_list);

                    long new_key = zobrist_hash_update(key, board, color, current_move.move);
                    movecount++;

                    current_variation = new pv_out();
                    move_order.add_current_move(current_move.move, board, 0);

                    //play the move
                    board = make_move(board, color, current_move.move, NNUE_avx2);

                    //copy the unmake move into move undo
                    MoveUndo = new int[MoveGenerator.UnmakeMove.Length];
                    Array.Copy(MoveGenerator.UnmakeMove, MoveUndo, MoveUndo.Length);

                    //calculate if the current move gives check
                    gives_check = MoveGenerator.CompleteCheck(board, color);

                    //find if the current position is a terminal position
                    //determining the mate value 2 => not a terminal position , 0 => draw , 1 => mate for white , -1 => mate for black
                    int matingValue = MoveGenerator.Mate(board, othercolor);

                    //checking if the position is not a terminal node
                    if (matingValue != 2)
                    {
                        //if the position is a terminal node the value for the node is set to the mating value from the perspective of the current color
                        current_variation.value = matingValue == 0 ? 0 : mate_value + max_depth;
                        current_variation.principalVariation.Insert(0, current_move.move);
                    }
                    else
                    {
                        if (!MoveGenerator.CompleteCheck(board, othercolor))
                        {
                            if (chess_stuff.is_capture(current_move.move , board))
                                position_is_quiet = false;
                            //if the current depth is 1 perform a quiescent search
                            if (current_depth <= 1)
                            {
                                current_variation.value = -quiescence_search(board, -window_b, -alpha, othercolor, NNUE_avx2, 0, 1, new_key);

                                current_variation.principalVariation.Add(current_move.move);
                            }
                            //else call the negamax function at the current depth minus 1
                            else
                            {
                                //perform a pv search
                                if (search_pv)
                                {
                                    current_variation = principal_variation_search(board, othercolor, current_depth - 1, 1, -window_b, -alpha, gives_check, NNUE_avx2, new_key);
                                    current_variation.value = -current_variation.value;
                                    current_variation.principalVariation.Insert(0, current_move.move);
                                }
                                else
                                {
                                    current_variation.value = -zero_window_search(board, othercolor, current_depth - 1, 1, -(alpha + 1), -alpha, gives_check, NNUE_avx2, new_key);

                                    if (stop)
                                    {
                                        //undo the move
                                        board = unmake_move(board, MoveUndo, currentacc, fifty_move_rule, NNUE_avx2);
                                        break;
                                    }

                                    if (current_variation.value > alpha)
                                    {
                                        current_variation = principal_variation_search(board, othercolor, current_depth - 1, 1, -window_b, -alpha, gives_check, NNUE_avx2, new_key);
                                        current_variation.value = -current_variation.value;
                                        current_variation.principalVariation.Insert(0, current_move.move);
                                    }
                                }
                            }
                        }
                    }

                    //undo the move
                    board = unmake_move(board, MoveUndo, currentacc, fifty_move_rule, NNUE_avx2);

                    //determine if the current move is better than the currently best move only if it is 
                    if (alpha < current_variation.value && current_variation.value != illegal_position_value)
                    {
                        if (chess_stuff.is_capture(current_move.move, board))
                            move_order.update_history_move(board, current_move.move, new int[0], new int[0], Math.Min((float)(depth * depth) / 10f, 40), color, 0);
                        else
                            move_order.update_chistory_move(board, current_move.move, color, Math.Min((float)(depth * depth) / 10f, 40));

                        alpha = current_variation.value;
                        pv = current_variation;
                        search_pv = false;
                    }

                    if (stop || alpha >= window_b)
                        break;
                }

                if (alpha <= window_a)
                {
                    delta_a *= 2;
                    window_a = last_last_best + delta_a;
                    alpha = window_a;
                }
                else if (alpha >= window_b)
                {
                    delta_b *= 2;
                    alpha = window_a;
                    window_b = last_last_best + delta_b;
                }
                else
                    break;
            }
            if (!stop)
            {
                Output.movelist.Add(pv.principalVariation[0]);
                Output.Scores.Add(chess_stuff.convert_millipawn_to_wdl(pv.value));
                Output.is_quiet = position_is_quiet;
                //add the best move to the hash table
                AddToTable(Output.movelist[0], current_depth, alpha, key, 0, 0);
            }
            //reset various variables
            last_last_best = last_best;
            last_best = alpha;
            alpha = -illegal_position_value;
            max_ply = 0;

            if (stop)
            {
                stop = false;
                break;
            }
        }

        //reset the nodecount
        Nodecount = 0;

        //stop the stopwatch
        sw.Stop();
        sw.Reset();


        return Output;
    }
    public byte[,] make_move(byte[,] board, byte color, int[] move, bool use_nnue)
    {
        //play the move
        board = MoveGenerator.PlayMove(board, color, move);

        //play the move in the accumulator
        if (use_nnue) ValueNet.update_acc_from_move(board, MoveGenerator.UnmakeMove);

        return board;
    }
    public byte[,] unmake_move(byte[,] board, int[] inverse_move, Accumulator acc, int fifty_move_counter, bool use_nnue)
    {
        //reset the fifty move counter
        MoveGenerator.fifty_move_rule = fifty_move_counter;

        //undo the current move
        board = MoveGenerator.UndoMove(board, inverse_move);

        //copy the old accumulator back in the real accumulator
        if (use_nnue) ValueNet.acc = chess_stuff.acc_copy(acc);

        return board;
    }
    public void AddToTable(int[] Move, int depth, int value, long key , byte beta_cutoff , byte alpha_cutoff)
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
            Log[i + 1] = BitConverter.GetBytes(value)[i];

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
        int eval = BitConverter.ToInt32(EvalParts);
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
        black_to_move = randomlong();
        piece_hashes[0] = new long[9, 9];
        //init the other pieces
        for (int i = 1; i < 27; i++)
        {
            piece_hashes[i] = new long[9, 9];
            for (int j = 0; j < 9; j++)
                for (int k = 0; k < 9; k++)
                {
                    if (i != 0)
                        piece_hashes[i][j, k] = randomlong();
                    else
                        piece_hashes[i][j, k] = 0;
                }
        }
        for (int color = 0; color < 17; color += 16)
        {
            //pawns
            piece_hashes[2 + color] = piece_hashes[1 + color];
            piece_hashes[3 + color] = piece_hashes[2 + color];

            //kings
            piece_hashes[6 + color] = piece_hashes[7 + color];

            //rooks
            piece_hashes[9 + color] = piece_hashes[10 + color];
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
            key = zobrist_hash(InputBoard, color);

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

        key = zobrist_hash(InputBoard, color);

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
    public long zobrist_hash(byte[,] InputBoard, byte color)
    {
        long Output = (1 - color) * black_to_move;

        for (int i = 1; i < 9; i++)
            for (int j = 1; j < 9; j++)
                Output ^= piece_hashes[InputBoard[i, j]][i, j];

        return Output;
    }
    public long zobrist_hash_update(long last_key, byte[,] board, byte color, int[] move)
    {
        if (move.Length == 4)
        {
            long output = last_key ^ black_to_move;

            //xor the target square 
            output ^= piece_hashes[board[move[2], move[3]]][move[2], move[3]];

            //xor the starting square
            output ^= piece_hashes[board[move[0], move[1]]][move[2], move[3]];

            //xor the starting square
            output ^= piece_hashes[board[move[0], move[1]]][move[0], move[1]];

            return output;
        }

        byte[,] new_board = new byte[9, 9];
        Array.Copy(board, new_board, board.Length);
        new_board = MoveGenerator.PlayMove(new_board, color, move);
        return zobrist_hash(new_board, (byte)(1 - color));
    }
    public int reduction_a(int depth, int movecount , bool pv_node)
    {
        double multiplier = 1;
        if (pv_node) multiplier = 2 / 3;

        return (byte)(multiplier * (Math.Sqrt(depth - 1) + Math.Sqrt(movecount - 1)));
    }
    public int reduction_b(int depth , int movecount, bool pv_node)
    {
        if (movecount > 6 && !pv_node)
            return 1;
        else
            return 0;
    }
    public int reduction(int depth, int movecount, bool pv_node)
    {
        return move_reductions[pv_node ? 0 : 1, Math.Min(depth, 63), Math.Min(movecount, 63)];
    }
    public void init_reductions()
    {
        for (int depth = 0; depth < 64; depth++)
        {
            for (int movecount = 0; movecount < 64; movecount++) 
            {
                if (depth > 3) move_reductions[1, depth, movecount] = (byte)(Math.Log(depth) * Math.Log(movecount) / 3 + 0.5);
                if (depth > 4) move_reductions[0, depth, movecount] = (byte)(Math.Log(depth) * Math.Log(movecount) / 3 - 0.2);
            }
        }
    }
    public int razoring_margin(int depth)
    {
        return 3500 * (depth * depth + 1);
    }
    public int reverse_futility_pruning_margin(int depth, bool improving)
    {
        int negator = improving ? 1 : 0;

        return 6000 * (2 * depth - negator);
    }
    public int extended_futility_pruning_margin(int depth , bool pv_node)
    {
        int add = pv_node ? 1 : 0;

        return 3000 * (2 * (depth + 1) + add);
    }
    public int move_pruning(int depthPly, bool improving)
    {
        int divisor = improving ? 2 : 3;

        return 6 + 18 * depthPly * depthPly / divisor;
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
}
class TTableEntry
{
    public int[] BestMove;
    public int Score;
    public byte depth;
    public bool fail_high = false , fail_low = false;
    public TTableEntry(int[] Bestmove, int CurrentScore, byte Currentdepth, bool cut_node, bool all_node)
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
    public bool is_quiet = false;
    public bool draw = false;
    public List<int[]> movelist = new List<int[]>();
    public List<float> Scores = new List<float>();
}
class pv_out
{
    public int value = -40000;
    public List<int[]> principalVariation = new List<int[]>();
}

