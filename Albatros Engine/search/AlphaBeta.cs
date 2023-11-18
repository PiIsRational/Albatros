using AlbatrosEngine.search;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
class AlphaBeta
{
    public ZobristHash hash = new ZobristHash();
    Stopwatch sw = new Stopwatch();
    Semaphore time_acces = new Semaphore(1, 1), depth_acces = new Semaphore(1, 1);
    bool stop = false;
    int[] sorting_counter = new int[300];

    public ulong[] repetitions = new ulong[307];
    bool[] repetionLookup = new bool[ushort.MaxValue];

    StandartChess chessStuff = new StandartChess();
    public NNUE_avx2 valueNet;
    public Movegen moveGenerator = new Movegen();
    public ClassicEval eval = new ClassicEval();
    public Move_Ordering_Heuristics moveOrder = new Move_Ordering_Heuristics();
    int nodecount = 0, max_ply = 0, moveCounter = 0, rootDepth = 1;
    int[,,] move_reductions = new int[2, 64, 64];
    bool[] nullMovePruning = new bool[byte.MaxValue + 1];
    public const int mateValue = 60000;
    public const int ILLEGAL_POSITION_VALUE = 80000;
    public const int MAX_DEPTH = 127;
    int[] node_values = new int[byte.MaxValue + 1];
    public long timeToUse = 0;
    public TTable tTable;
    private ReverseMove[] reverseMoves = new ReverseMove[307];

    public AlphaBeta(int hashSize)
    {
        for (int i = 0; i< 307; i++)
            reverseMoves[i] = new ReverseMove();

        tTable = new TTable(hashSize);
        init_reductions(3, 2, 2, 0, 1, 1, -0.2, 0.5);
        //reduction_b();
        valueNet = new NNUE_avx2(true);
    }

    public void Stop()
    {
        stop = true;
    }

    public int TimedAlphaBeta(long milliseconds, Position board, bool NNUE_avx2, bool changeTime)
    {
        timeToUse = milliseconds;
        Thread timer = new Thread(ThreadTimer);
        timer.Start();
        return IterativeDeepening(board, MAX_DEPTH, NNUE_avx2, changeTime);
    }
    public void ThreadTimer()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        long timeToSearch = timeToUse;
        int searchDepth = 1;
        while (stopwatch.ElapsedMilliseconds <= timeToSearch || searchDepth == 1)
        {
            Thread.Sleep(1);
            depth_acces.WaitOne();
            searchDepth = rootDepth;
            depth_acces.Release();

            time_acces.WaitOne();
            timeToSearch = timeToUse;
            time_acces.Release();
        }
        Stop();
    }
    public int IterativeDeepening(Position board, int depth, bool useNNUE, bool changeTime)
    {
        //initialize the variables
        moveOrder.resetMovesort();
        int[] movelist = new int[218];
        stop = false;
        byte othercolor = (byte)(board.color ^ 1);
        List<int[]> lastBestMoves = new List<int[]>();
        bool search_pv = true, inCheck = moveGenerator.check(board, false), givesCheck = false;
        int Output = 0, new_depth = 0;
        movelist = moveGenerator.LegalMoveGenerator(board, inCheck, reverseMoves[0], movelist);
        int movelist_length = moveGenerator.moveIdx;
        PvOut current_variation, pv = new PvOut();
        int alpha = -ILLEGAL_POSITION_VALUE, delta_a = 0, delta_b = 0, window_a = 0, window_b = 0, last_best = 0, last_last_best = 0, currentScore = 0;
        bool pruning_is_safe = false;
        MoveList move_list = new MoveList();
        time_acces.WaitOne();
        long theoretical_time_usage = timeToUse;
        time_acces.Release();
        //get the key for the position
        ulong key = hash.HashPosition(board);

        //start the stopwatch
        sw.Start();
        sw.Start();

        if (useNNUE)
        {
            //the the accumulator position to the starting position
            valueNet.set_acc_from_position(board);

            //copy the accumulator for the current position
            currentScore = valueNet.AccToOutput(valueNet.acc, board.color);
        }
        else
            currentScore = eval.PestoEval(board);

        pruning_is_safe = !inCheck && moveGenerator.nonPawnMaterial;

        node_values[0] = !inCheck ? currentScore : ILLEGAL_POSITION_VALUE;

        for (int current_depth = 1; current_depth <= depth; current_depth++)
        {
            depth_acces.WaitOne();
            rootDepth = current_depth;
            depth_acces.Release();
            node_values[0] = !inCheck ? (tTable.IsValid(key) == 1 ? tTable.GetInfo(key).Score : currentScore) : ILLEGAL_POSITION_VALUE;

            if (current_depth >= 4 && Math.Abs(last_last_best) < mateValue)
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
                window_a = -ILLEGAL_POSITION_VALUE;
                window_b = ILLEGAL_POSITION_VALUE;
                alpha = window_a;
            }

            while (!stop)
            {
                search_pv = true;

                move_list = moveOrder.EvaluateMoves(board, Standart.CLone(movelist), movelist_length, 0, false, tTable.IsValid(key) == 1 ? tTable.GetInfo(key).BestMove : 0, move_list);
                while (move_list.Length > 0)
                {

                    Movepick current_move = moveOrder.PickNextMove(move_list);
                    //Debug.Assert(!stuff.int_array_equal(current_move.move, new int[] { 4, 5, 4, 4 }) || current_depth != 3);

                    current_variation = new PvOut();
                    moveOrder.add_current_move(current_move.move, board, 0);

                    //play the move
                    board = MakeMove(board, current_move.move, useNNUE, reverseMoves[0]);

                    //get the hash key
                    ulong new_key = hash.UpdateHashAfterMove(board, reverseMoves[0], key);

                    //calculate if the current move gives check
                    givesCheck = moveGenerator.FastCheck(board, current_move.move);

                    //find if the current position is a terminal position
                    //determining the mate value 2 => not a terminal position , 0 => draw , 1 => mate for white , -1 => mate for black
                    int matingValue = moveGenerator.is_mate(board, givesCheck, new ReverseMove(), new int[214]);

                    new_depth = current_depth - 1;

                    //checking if the position is not a terminal node
                    if (matingValue != 2)
                    {
                        //if the position is a terminal node the value for the node is set to the mating value from the perspective of the current color
                        current_variation.value = matingValue == 0 ? 0 : mateValue + MAX_DEPTH;
                        current_variation.principalVariation.Insert(0, current_move.move);
                    }
                    else
                    {
                        //if the current depth is 1 perform a quiescent search
                        if (new_depth <= 0)
                        {
                            current_variation.value = -QuiescenceSearch(board, -window_b, -alpha, useNNUE, 0, 1, new_key, givesCheck);

                            current_variation.principalVariation.Add(current_move.move);
                        }
                        //else perform a normal pv search
                        else
                        {
                            //perform a pv search
                            if (search_pv)
                            {
                                current_variation = principal_variation_search(board, new_depth, 1, -window_b, -alpha, givesCheck, useNNUE, new_key);
                                current_variation.value = -current_variation.value;
                                current_variation.principalVariation.Insert(0, current_move.move);
                            }
                            else
                            {
                                current_variation.value = -ZeroWindowSearch(board, new_depth, 1, -(alpha + 1), -alpha, givesCheck, useNNUE, new_key);

                                if (current_variation.value > alpha && current_variation.value < window_b)
                                {
                                    current_variation = principal_variation_search(board, new_depth, 1, -window_b, -alpha, givesCheck, useNNUE, new_key);
                                    current_variation.value = -current_variation.value;
                                    current_variation.principalVariation.Insert(0, current_move.move);
                                }
                            }
                        }
                    }

                    //undo the move
                    board = UnmakeMove(board, reverseMoves[0], useNNUE);

                    if (alpha < current_variation.value)
                    {
                        if (chessStuff.is_capture(current_move.move, board))
                            moveOrder.update_history_move(board, current_move.move, 0, 0, 0, 0, Math.Min((float)(depth * depth) / 10f, 40), 0);
                        else
                            moveOrder.update_chistory_move(board, current_move.move, Math.Min((float)(depth * depth) / 10f, 40));

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
                Output = pv.principalVariation[0];

                //adjust timing
                time_acces.WaitOne();
                if (current_depth > 2 && changeTime)
                {
                    //if the timing is already maximal do not change it
                    //else if the best move is different to the last best move make the time usage larger
                    if (theoretical_time_usage * 14 > timeToUse * 10 && Output != tTable.GetInfo(key).BestMove && theoretical_time_usage < 500)
                        timeToUse += theoretical_time_usage / 10;
                    //else if it is really low do not change it
                    //else make the time usage smaller
                    else if (theoretical_time_usage * 4 < timeToUse * 10)
                        timeToUse -= theoretical_time_usage / 10;
                }
                time_acces.Release();

                //add the best move to the hash table
                tTable.Add(Output, (byte)current_depth, alpha, key, false, false);
            }
            //after a finished search return the main information 
            if (!stop)
            {
                if (Math.Abs(alpha) < mateValue)
                    Console.WriteLine("info depth {2} seldepth {3} nodes {1} nps {4} time {5} hashfull {7} score cp {0} pv {6}", alpha / 10, nodecount, current_depth, max_ply + 1, (int)(((float)(nodecount) * 1000) / (sw.ElapsedMilliseconds > 0 ? (float)sw.ElapsedMilliseconds : 1)), (int)(sw.ElapsedMilliseconds), variation_to_string(pv.principalVariation), (tTable.EntryCount() * 1000) / tTable.Size);
                else
                    Console.WriteLine("info depth {2} seldepth {3} nodes {1} nps {4} time {5} hashfull {7} score mate {0} pv {6}", -(alpha - (alpha / Math.Abs(alpha)) * (MAX_DEPTH + mateValue + 1)) / 2, nodecount, current_depth, current_depth + max_ply, (int)(((float)(nodecount) * 1000) / (sw.ElapsedMilliseconds > 0 ? (float)sw.ElapsedMilliseconds : 1)), (int)(sw.ElapsedMilliseconds), variation_to_string(pv.principalVariation), (tTable.EntryCount() * 1000) / tTable.Size);
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
        nodecount = 0;

        //stop the stopwatch
        sw.Stop();
        sw.Reset();

        //return the best move
        return Output;
    }
    public PvOut principal_variation_search(Position board, int depth, int ply, int alpha, int beta, bool inCheck, bool NNUE_avx2, ulong key)
    {
        //define the variables
        bool search_pv = true, givesCheck = false, twoFoldRepetition = false, improving = true, full_depth_search = false, fail_low = false, pruningSafe = false;
        int BestMove = -1;
        int movecount = 0, interestingMoveCount, newDepth;
        int staticScore = -ILLEGAL_POSITION_VALUE, score = -ILLEGAL_POSITION_VALUE, decrease = 0, improvement = 0;
        PvOut Output = new PvOut(), current_variation;
        Output.value = alpha;
        TranspositionTableEntry entry = new TranspositionTableEntry(0, 0, 0, false, false, false, key);
        MoveList move_list = new MoveList();
        int[] played_moves = new int[214];
        int played_move_idx = 0;

        if (board.fiftyMoveRule == 100 || stop)
        {
            Output.value = 0;
            return Output;
        }

        //threefold repetition
        if (InFastLookup(key))
        {
            if (repetition_count(key) == 2)
            {
                Output.value = 0;
                return Output;
            }
            else if (repetition_count(key) == 1)
                twoFoldRepetition = true;
        }

        int keyValid = tTable.IsValid(key);

        if (keyValid > -2)
        {
            entry = tTable.GetInfo(key);

            if (keyValid == 1)
            {
                if (Math.Abs(entry.Score) >= mateValue)
                    entry.Score -= entry.Score / Math.Abs(entry.Score) * ply;

                //if the position has the right depth return the value of the position
                if (entry.Depth >= depth)
                {
                    if (entry.Score >= beta && !entry.FailLow)
                    {
                        Output.value = beta;
                        return Output;
                    }
                    if (entry.Score <= alpha && !entry.FailHigh)
                    {
                        Output.value = alpha;
                        return Output;
                    }
                }
            }
        }

        int[] moves = moveGenerator.LegalMoveGenerator(board, inCheck, reverseMoves[ply], new int[214]);
        int movelist_length = moveGenerator.moveIdx;
        int curr_node_count = nodecount;

        AddPositionToLookups(key);

        if (!inCheck)
        {
            if (NNUE_avx2)
                staticScore = valueNet.AccToOutput(valueNet.acc, board.color);
            else
                staticScore = eval.PestoEval(board);

            staticScore = eval.DrawScore(board, staticScore);
        }

        if (keyValid == 1 && entry.ExactScore)
            score = entry.Score;
        else
            score = staticScore;

        /* update the value in the value array
         * if we are in check do not update the value*/

        node_values[ply] = !inCheck ? score : ILLEGAL_POSITION_VALUE;

        //set the improving flag high if the current value is an improvement
        improvement = ply < 2 ? 0 : node_values[ply] - node_values[ply - 2];
        improving = (improvement > 0 || ply < 2) && !inCheck;

        /*we should be able to prune branches only in specific cases
         * when we are not in check
         * and when the depth of the root is larger then 3
         */

        //sort the moves
        move_list = moveOrder.EvaluateMoves(board, moves, movelist_length, ply, false, keyValid == 1 ? entry.BestMove : BestMove, move_list);
        interestingMoveCount = moveOrder.tacticalMoveCounter;

        pruningSafe = !inCheck && rootDepth > 3 &&
                StandartChess.NonMateWindow(alpha, beta) &&
                moveGenerator.nonPawnMaterial;

        while (move_list.Length > 0)
        {
            Movepick currentMove = moveOrder.PickNextMove(move_list);

            //find futile moves
            if (movecount > 2 &&
                currentMove.eval < 4000 &&
                pruningSafe &&
                depth < 8)
            {
                //late move pruning
                if (movecount >= MovePruning(depth, improving) + interestingMoveCount)
                    break;

                //if lmr is allowed
                if (depth > 2 && movecount > interestingMoveCount)
                {
                    //calculate the depth of the late move reduction
                    int lmrDepth = depth - (1 + Reduction(depth, movecount - interestingMoveCount, true));

                    //futility pruning
                    if (staticScore + EfpMargin(lmrDepth, true) < alpha)
                        break;
                    //history pruning
                    if (currentMove.eval < HistoryMargin(lmrDepth, improving))
                        break;
                }
            }

            movecount++;

            current_variation = new PvOut();
            moveOrder.add_current_move(currentMove.move, board, ply);

            //play the move
            board = MakeMove(board, currentMove.move, NNUE_avx2, reverseMoves[ply]);

            ulong newKey = hash.UpdateHashAfterMove(board, reverseMoves[ply], key);

            //calculate if the current move gives check
            givesCheck = moveGenerator.FastCheck(board, currentMove.move);

            //set the new depth
            newDepth = depth - 1;

            /*calculate depth extensions*/

            //check extension
            if (inCheck)
                newDepth++;

            //if the current depth is 1 do a quiescent search
            if (newDepth <= 0)
            {
                current_variation.value = -QuiescenceSearch(board, -beta, -alpha, NNUE_avx2, 0, ply + 1, newKey, givesCheck);
                current_variation.principalVariation.Add(currentMove.move);
            }
            //else just call the function recursively
            else
            {
                if (search_pv)
                {
                    current_variation = principal_variation_search(board, newDepth, ply + 1, -beta, -alpha, givesCheck, NNUE_avx2, newKey);
                    current_variation.value = -current_variation.value;
                    current_variation.principalVariation.Insert(0, currentMove.move);
                }
                else
                {
                    //late move reduction
                    if (depth > 2 && movecount > 1 && movecount > interestingMoveCount && !inCheck && !givesCheck)
                    {
                        decrease = Reduction(newDepth, movecount - interestingMoveCount, true);

                        //if we are improving or not decrease more or less
                        if (!improving) decrease += 1;

                        //at least a counter move
                        if (currentMove.eval >= 4000)
                            decrease -= 1;

                        //normal quiet move reduction
                        else
                            decrease -= (int)Math.Max(Math.Min(currentMove.eval / 500, 2), -2);

                        int lmrDepth = Math.Max(Math.Min(newDepth, newDepth - decrease), 1);

                        current_variation.value = -ZeroWindowSearch(board, lmrDepth, ply + 1, -(alpha + 1), -alpha, givesCheck, NNUE_avx2, newKey);

                        if (current_variation.value > alpha && lmrDepth < newDepth && current_variation.value != ILLEGAL_POSITION_VALUE)
                            full_depth_search = true;
                    }
                    else
                        full_depth_search = true;

                    if (full_depth_search)
                    {
                        current_variation.value = -ZeroWindowSearch(board, newDepth, ply + 1, -(alpha + 1), -alpha, givesCheck, NNUE_avx2, newKey);

                        full_depth_search = false;
                    }

                    if (current_variation.value > alpha && current_variation.value < beta)
                    {
                        current_variation = principal_variation_search(board, newDepth, ply + 1, -beta, -alpha, givesCheck, NNUE_avx2, newKey);
                        current_variation.value = -current_variation.value;
                        current_variation.principalVariation.Insert(0, currentMove.move);
                    }
                }
            }

            //undo the move
            board = UnmakeMove(board, reverseMoves[ply], NNUE_avx2);
            played_moves[played_move_idx] = currentMove.move;
            played_move_idx++;

            if (current_variation.value > alpha)
            {
                alpha = current_variation.value;
                BestMove = currentMove.move;
                Output = current_variation;
                search_pv = false;

                //if the branch is not better then the currently best branch we can prune the other positions
                if (alpha >= beta)
                {
                    //store the killer move history moves and counter moves
                    moveOrder.UpdateHistories(board, currentMove.move, played_moves, played_move_idx, nullMovePruning, depth, ply, true);

                    //add the best move to the hash table if the current depth is greater than the depth of the entry or there is no entry in the hash table
                    if (!stop && (keyValid == -2 || entry.Depth <= depth))
                        tTable.Add(currentMove.move, (byte)depth, beta, key, true, false);

                    RemovePositionFromLookups(key, twoFoldRepetition);

                    Output.principalVariation = new List<int>();
                    Output.value = beta;
                    return Output;
                }

                //store the killer move history moves and counter moves
                moveOrder.UpdateHistories(board, currentMove.move, played_moves, played_move_idx, nullMovePruning, depth, ply, false);
            }

        }

        RemovePositionFromLookups(key, twoFoldRepetition);

        //if no move was legal return the Score for mate
        if (movelist_length == 0)
        {
            //mate
            if (inCheck)
            {
                Output.value = -(mateValue + MAX_DEPTH - ply);
                tTable.Add(0, MAX_DEPTH, Output.value, key, false, false);
                Output.principalVariation = new List<int>();
                return Output;
            }
            //stalemate
            Output.value = 0;
            tTable.Add(0, MAX_DEPTH, Output.value, key, false, false);
            Output.principalVariation = new List<int>();
            return Output;
        }

        //if we have not managed to exceed alpha we have not found the best move so we use the first move we searched
        if (BestMove == -1)
        {
            fail_low = true;
            BestMove = played_moves[0];
        }

        //add the best move to the hash table if the current depth is greater than the depth of the entry or thre is no entry in the hash table
        if (!stop && (keyValid == -2 || entry.Depth <= depth || !fail_low))
            tTable.Add(BestMove, (byte)depth, Math.Abs(alpha) > mateValue ? alpha + Math.Abs(alpha) / alpha * ply : alpha, key, false, fail_low);

        //return the best Score
        return Output;
    }
    public int ZeroWindowSearch(Position board, int depth, int ply, int alpha, int beta, bool inCheck, bool NNUE_avx2, ulong key)
    {
        //define the variables
        int current_score = 0, staticScore = -ILLEGAL_POSITION_VALUE, score = -ILLEGAL_POSITION_VALUE, decrease = 0, improvement = 0;
        byte othercolor = (byte)(board.color ^ 1);
        bool givesCheck = false, twoFoldRepetition = false, full_depth_search = false;
        int movecount = 0, interestingMoveCount, new_depth = 0;
        bool improving = false, pruningIsSafe = false;
        TranspositionTableEntry entry = new TranspositionTableEntry(0, 0, 0, false, false, false, key);
        MoveList moveList = new MoveList();
        int[] playedMoves = new int[214];
        int playedMoveIdx = 0;

        if (board.fiftyMoveRule == 100 || stop)
            return 0;

        //threefold repetition
        if (InFastLookup(key))
        {
            if (repetition_count(key) == 2)
                return 0;
            if (repetition_count(key) == 1)
                twoFoldRepetition = true;
        }

        int keyValid = tTable.IsValid(key);

        if (keyValid > -2)
        {
            entry = tTable.GetInfo(key);

            if (keyValid == 1)
            {
                if (Math.Abs(entry.Score) >= mateValue)
                    entry.Score -= entry.Score / Math.Abs(entry.Score) * ply;
                //if the position has the right depth we can use the value of the position
                if (entry.Depth >= depth)
                {
                    //if the Score is larger or equal to beta we can return beta
                    if (entry.Score >= beta && !entry.FailLow)
                        return beta;
                    //else if the Score is certain and it is smaller then alpha we have an alpha cutoff
                    if (entry.Score <= alpha && !entry.FailHigh)
                        return alpha;
                    if (entry.ExactScore)
                        return entry.Score;
                }
            }
        }

        int[] moves = moveGenerator.LegalMoveGenerator(board, inCheck, reverseMoves[ply], new int[214]);
        int movelist_length = moveGenerator.moveIdx;

        if (!inCheck)
        {
            if (NNUE_avx2)
                staticScore = valueNet.AccToOutput(valueNet.acc, board.color);
            else
                staticScore = eval.PestoEval(board);

            staticScore = eval.DrawScore(board, staticScore);
        }

        if (keyValid == 1 && entry.ExactScore)
            score = entry.Score;
        else
            score = staticScore;

        /* update the value in the value array
         * if we are in check do not update the value*/
        node_values[ply] = !inCheck ? score : ILLEGAL_POSITION_VALUE;

        //set the improving flag high if the current value is an improvement
        improvement = ply < 2 ? 0 : node_values[ply] - node_values[ply - 2];
        improving = (improvement > 0 || ply < 2) && !inCheck;

        /*we should be able to prune branches only in specific cases
         * when we are not in check
         * and when the depth of the root is larger then 3
         */

        pruningIsSafe = !inCheck && rootDepth > 3 &&
            StandartChess.NonMateWindow(alpha, beta) &&
            moveGenerator.nonPawnMaterial;

        if (pruningIsSafe)
        {
            /*Razoring
             * 
             * if the current Score is really bad,
             * we try a quiescence search to look if a tactical sequence can make up for the bad Score
             * if this is not the case we just prune 
             */
            if (depth < 4 &&
                score + RazoringMargin(depth, improving) < alpha &&
                (keyValid == 1 && !entry.FailHigh))
            {
                int test_value = QuiescenceSearch(board, alpha - 1, alpha, NNUE_avx2, 0, ply + 1, key, inCheck);

                if (test_value < alpha)
                    return test_value;
            }

            //Reverse Futility Pruning
            if (depth < 7 &&
                score - RFPMargin(depth) >= beta &&
                (keyValid != 1 || !entry.FailLow))
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
                score >= beta &&
                staticScore >= beta &&
                !nullMovePruning[ply - 1] &&
                (keyValid != 1 || !entry.FailLow))
            {
                int nmpScore;

                /* calculate the depth for the null move search:
                 *
                 * 1) the base depth reduction is 3
                 * 2) else the depth gets reduced by a factor 1/6
                 * 3) the larger the delta between the standing pat and beta the more we can reduce
                 */

                int null_move_search_depth = depth - 1 - (3 + depth / 6 + Math.Min(3, (score - beta) / 650));

                board = Movegen.MakeNullMove(board, reverseMoves[ply]);

                if (null_move_search_depth <= 0)
                    nmpScore = -QuiescenceSearch(board, -beta, -alpha, NNUE_avx2, 0, ply + 1, hash.update_null_move_hash(key, reverseMoves[ply]), false);
                else
                {
                    //add the null move search to the table
                    nullMovePruning[ply] = true;

                    nmpScore = -ZeroWindowSearch(board, null_move_search_depth, ply + 1, -beta, -alpha, false, NNUE_avx2, hash.update_null_move_hash(key, reverseMoves[ply]));

                    //remove the null move search from the table
                    nullMovePruning[ply] = false;
                }

                board = moveGenerator.unmake_move(board, reverseMoves[ply]);

                /*if the new Score is better
                 *and the next position is not illegal
                 *and the next value is not mate
                 *return beta
                 */
                if (nmpScore >= beta)
                    return beta;
            }
        }

        //sort the moves
        moveList = moveOrder.EvaluateMoves(board, moves, movelist_length, ply, false, keyValid == 1 ? entry.BestMove : 0, moveList);
        interestingMoveCount = moveOrder.tacticalMoveCounter;

        AddPositionToLookups(key);

        while (moveList.Length > 0)
        {
            Movepick currentMove = moveOrder.PickNextMove(moveList);

            //find futile moves
            if (movecount > 2 &&
                currentMove.eval < 4000 &&
                pruningIsSafe &&
                depth < 8)
            {
                //late move pruning
                if (movecount >= MovePruning(depth, improving) + interestingMoveCount)
                    break;

                //if lmr is allowed
                if (depth > 2 && movecount > interestingMoveCount)
                {
                    //calculate tthe depth of the late move reduction
                    int lmrDepth = depth - (1 + Reduction(depth, movecount - interestingMoveCount, true));

                    //futility pruning
                    if (staticScore + EfpMargin(lmrDepth, true) < alpha)
                        break;

                    //history pruning
                    if (currentMove.eval < HistoryMargin(lmrDepth, improving))
                        break;
                }

            }
            movecount++;
            moveOrder.add_current_move(currentMove.move, board, ply);

            //play the move
            board = MakeMove(board, currentMove.move, NNUE_avx2, reverseMoves[ply]);

            ulong new_key = hash.UpdateHashAfterMove(board, reverseMoves[ply], key);

            //calculate if the current move gives check
            givesCheck = moveGenerator.FastCheck(board, currentMove.move);

            //set the new depth
            new_depth = depth - 1;

            /*calculate depth depth extentions*/

            //check extention
            if (inCheck)
                new_depth++;

            //if the current depth is 1 do a quiescent search
            if (new_depth <= 0)
                current_score = -QuiescenceSearch(board, -beta, -alpha, NNUE_avx2, 0, ply + 1, new_key, givesCheck);

            //else just call the function recursively
            else
            {
                //late move reduction
                if (depth > 2 && movecount > 1 && movecount > interestingMoveCount && !inCheck && !givesCheck)
                {
                    //calculate a base reduction
                    decrease = Reduction(new_depth, movecount - interestingMoveCount, false);

                    if (!improving) decrease += 1;

                    if (givesCheck)
                        decrease -= 1;

                    //at least a counter move
                    if (currentMove.eval >= 4000)
                        decrease -= 1;

                    //normal quiet move reduction
                    else
                        decrease -= (int)Math.Max(Math.Min(currentMove.eval / 500, 2), -2);

                    int lmr_depth = Math.Max(Math.Min(new_depth, new_depth - decrease), 1);

                    current_score = -ZeroWindowSearch(board, lmr_depth, ply + 1, -beta, -alpha, givesCheck, NNUE_avx2, new_key);

                    if (current_score > alpha && lmr_depth < new_depth)
                        full_depth_search = true;
                }
                else
                    full_depth_search = true;

                if (full_depth_search)
                {
                    current_score = -ZeroWindowSearch(board, new_depth, ply + 1, -beta, -alpha, givesCheck, NNUE_avx2, new_key);

                    full_depth_search = false;
                }
            }

            //undo the move
            board = UnmakeMove(board, reverseMoves[ply], NNUE_avx2);

            playedMoves[playedMoveIdx] = currentMove.move;
            playedMoveIdx++;

            //if the branch is not better then the currently best branch we can prune the other positions
            if (current_score >= beta)
            {
                //store the killer move history moves and counter moves
                moveOrder.UpdateHistories(board, currentMove.move, playedMoves, playedMoveIdx, nullMovePruning, depth, ply, true);

                //add the best move to the hash table if the current depth is greater than the depth of the entry or thre is no entry in the hash table
                if (!stop && (keyValid == -2 || entry.Depth <= depth))
                    tTable.Add(currentMove.move, (byte)depth, beta, key, true, false);

                RemovePositionFromLookups(key, twoFoldRepetition);

                return beta;
            }
        }

        RemovePositionFromLookups(key, twoFoldRepetition);

        //if no move was legal return the Score for a terminal node
        if (movelist_length == 0)
        {
            //mate
            if (inCheck)
            {
                tTable.Add(0, MAX_DEPTH, -(mateValue + MAX_DEPTH - ply), key, false, false);
                return -(mateValue + MAX_DEPTH - ply);
            }

            //stalemate
            tTable.Add(0, MAX_DEPTH, 0, key, false, false);
            return 0;
        }

        //add the best move to the hash table if there is no entry in the hash table
        if (!stop && (keyValid == -2 || entry.Depth <= depth)) 
            tTable.Add(playedMoves[0], (byte)depth, Math.Abs(alpha) >= mateValue ? alpha + Math.Abs(alpha) / alpha * ply : alpha, key, false, true);

        return alpha;
    }

    public int QuiescenceSearch(Position board, int alpha, int beta, bool NNUE_avx2, int depth, int ply, ulong key, bool inCheck)
    {
        if (stop)
            return 0;

        //look for repetitions
        if (depth == 0)
        {
            //threefold repetition
            if (InFastLookup(key) && repetition_count(key) == 2 || board.fiftyMoveRule == 100)
                return 0;
        }

        //define the variables
        nodecount++;
        int standingPat = -ILLEGAL_POSITION_VALUE, currentScore = 0, bestMove = 0, moveCount = 0;
        byte othercolor = (byte)(board.color ^ 1);
        bool failLow = true, givesCheck;
        int[] moves;
        MoveList moveList = new MoveList();
        TranspositionTableEntry entry = new TranspositionTableEntry(0, 0, 0, false, false, false, key);
        int keyValid = tTable.IsValid(key);

        if (keyValid > -2)
        {
            entry = tTable.GetInfo(key);

            if (keyValid == 1)
            {
                if (Math.Abs(entry.Score) >= mateValue)
                    entry.Score -= entry.Score / Math.Abs(entry.Score) * ply;

                //if the Score is larger or equal to beta we can return beta
                if (entry.Score >= beta && entry.FailHigh)
                    return beta;

                //else if the Score is certain and it is smaller then alpha we have an alpha cutoff
                if (entry.Score <= alpha && entry.FailLow)
                    return alpha;

                if (entry.ExactScore)
                    return entry.Score;
            }
        }

        //if we are in check look for  other moves
        if (inCheck)
            moves = moveGenerator.LegalMoveGenerator(board, inCheck, reverseMoves[ply], new int[214]);
        //else just look for captures
        else
            moves = moveGenerator.LegalCaptureGenerator(board, inCheck, reverseMoves[ply], new int[100]);

        int movelistLength = moveGenerator.moveIdx;

        //if we are in check standing pat is no allowed because we search all moves and not only captures
        if (!inCheck)
        {
            if (NNUE_avx2)
                standingPat = valueNet.AccToOutput(valueNet.acc, board.color);
            else
                standingPat = eval.PestoEval(board);

            standingPat = eval.DrawScore(board, standingPat);
        }

        //if the branch is not better then the currently best branch we can prune the other positions
        if (standingPat >= beta)
            return beta;

        //delta pruning
        if (standingPat < alpha - 11000 && !inCheck)
            return alpha;

        if (standingPat > alpha)
            alpha = standingPat;

        //if the position is quiet return the evaluation
        if (movelistLength == 0)
        {
            //we count the first
            max_ply = Math.Max(ply - 1, max_ply);

            //if there is no legal move it is checkmate
            if (inCheck)
            {
                alpha = -(mateValue + MAX_DEPTH - ply);
                tTable.Add(0, MAX_DEPTH, alpha, key, false, false);
            }
            else if (!stop && keyValid == -2)
                // the real evalutaion
                tTable.Add(0, 0, standingPat, key, false, false);

            return alpha;
        }

        //sort the moves
        moveList = moveOrder.EvaluateMoves(board, moves, movelistLength, ply, true, keyValid == 1 ? entry.BestMove : 0, moveList);

        while (moveList.Length > 0)
        {
            Movepick currentMove = moveOrder.PickNextMove(moveList);

            if (moveGenerator.nonPawnMaterial && !inCheck && (currentMove.eval - 10000) / 3 + standingPat + 2000 < alpha)
                break;

            moveCount++;

            //play the move
            board = MakeMove(board, currentMove.move, NNUE_avx2, reverseMoves[ply]);

            ulong newKey = hash.UpdateHashAfterMove(board, reverseMoves[ply], key);

            //calculate if the current move gives check
            givesCheck = moveGenerator.FastCheck(board, currentMove.move);

            //calls itself recursively
            currentScore = -QuiescenceSearch(board, -beta, -alpha, NNUE_avx2, depth + 1, ply + 1, newKey, givesCheck);

            //undo the move
            board = UnmakeMove(board, reverseMoves[ply], NNUE_avx2);

            if (moveCount == 0) bestMove = currentMove.move;

            if (currentScore > alpha)
            {
                failLow = false;
                alpha = currentScore;
                bestMove = currentMove.move;

                //if the branch is not better then the currently best branch we can prune the other positions
                if (currentScore >= beta)
                {
                    if (!stop && keyValid == -2)
                        tTable.Add(bestMove, 0, beta, key, true, false);
                    return beta;
                }
            }
        }

        //add the best move to the hash table if the current depth is greater than the depth of the entry or thre is no entry in the hash table
        if (!stop && keyValid == -2)
            tTable.Add(bestMove, 0, Math.Abs(alpha) >= mateValue ? alpha + Math.Abs(alpha) / alpha * ply : alpha, key, false, failLow);

        //return the best Score
        return alpha;
    }
   
    public Position MakeMove(Position board, int move, bool useNNUE, ReverseMove inverseMove)
    {
        //play the move
        board = moveGenerator.MakeMove(board, move, true, inverseMove);

        //play the move in the accumulator
        if (useNNUE) valueNet.UpdateAccFromMove(board, inverseMove, false);

        return board;
    }
    public Position UnmakeMove(Position board, ReverseMove inverseMove, bool useNNUE)
    {
        //copy the old accumulator back in the real accumulator
        if (useNNUE) valueNet.UpdateAccFromMove(board, inverseMove, true);

        //undo the current move
        board = moveGenerator.unmake_move(board, inverseMove);

        return board;
    }

    public void AddPositionToLookups(ulong key)
    {
        //add to fast lookup
        repetionLookup[key % (ulong)repetionLookup.Length] = true;

        //add to move array
        repetitions[moveCounter] = key;

        moveCounter++;
    }
    public bool InFastLookup(ulong key)
    {
        return repetionLookup[key % (ulong)repetionLookup.Length];
    }
    public int repetition_count(ulong key)
    {
        int count = 0;
        for (int i = 0; i <= moveCounter; i++)
        {
            if (repetitions[i] == key)
                count++;
        }

        return count;
    }
    public Position PlayGameFromMoves(Position board, int[] moves)
    {
        board.fiftyMoveRule = 0;
        moveCounter = 0;
        ulong key = 0;
        int[] pseudolegal_movelist = new int[218];
        key = hash.HashPosition(board);

        AddPositionToLookups(key);

        for (int i = 0; i < moves.Length; i++)
        {
            int move = moves[i];
            byte other = (byte)(move >> 12);
            if (other == 0)
            {
                pseudolegal_movelist = moveGenerator.generate_movelist(board, pseudolegal_movelist);
                int movelist_length = moveGenerator.moveIdx;
                for (int j = 0; j < movelist_length; j++)
                    if ((pseudolegal_movelist[j] & 0b0000111111111111) == move)
                        move = pseudolegal_movelist[j];
            }

            board = PlayMove(board, move, false, null);
        }


        return board;
    }
    public Position PlayMove(Position board, int move, bool useReverseMove, ReverseMove undoMove)
    {
        board = moveGenerator.MakeMove(board, move, useReverseMove, undoMove);

        if (board.fiftyMoveRule == 0)
            ResetLookups();

        ulong key = hash.HashPosition(board);

        AddPositionToLookups(key);

        return board;
    }
    public void ResetLookups()
    {
        moveCounter = 0;
        repetionLookup = new bool[ushort.MaxValue];
    }
    public void RemovePositionFromLookups(ulong key, bool twoFoldRep)
    {
        //remove from fast lookup
        if (!twoFoldRep)
            repetionLookup[key % (ulong)repetionLookup.Length] = false;

        //derease the move counter
        moveCounter--;
    }
    public int reduction_a(int depth, int movecount, bool pv_node)
    {
        double multiplier = 1;
        if (pv_node) multiplier = 2 / 3;

        return (byte)(multiplier * (Math.Sqrt(depth - 1) + Math.Sqrt(movecount - 1)));
    }
    public void reduction_b()
    {
        int reduction;

        for (int depth = 2; depth < 64; depth++)
        {
            for (int movecount = 3; movecount < 64; movecount++)
            {
                reduction = (int)Math.Sqrt((double)((depth - 2) * (movecount - 3) / 12));

                if (movecount <= 4)
                    reduction = Math.Min(reduction, 1);

                move_reductions[1, depth, movecount] = reduction;
                move_reductions[0, depth, movecount] = Math.Max(reduction - 1, 0);
            }
        }
    }
    public int Reduction(int depth, int movecount, bool pv_node)
    {
        return move_reductions[pv_node ? 0 : 1, Math.Min(depth, 63), Math.Min(movecount, 63)];
    }
    public void init_reductions(int min_depth_pv, int min_depth, int min_moves_pv, int min_moves, double pv_divisor, double divisor, double pv_add, double add)
    {
        for (int depth = 0; depth < 64; depth++)
        {
            for (int movecount = 0; movecount < 64; movecount++)
            {
                if (depth > min_depth && movecount > min_moves) move_reductions[1, depth, movecount] = (byte)(Math.Log(depth) * Math.Log(movecount) / divisor + add);
                if (depth > min_depth_pv && movecount > min_moves_pv) move_reductions[0, depth, movecount] = (byte)(Math.Log(depth) * Math.Log(movecount) / pv_divisor + pv_add);
            }
        }
    }

    public static int RazoringMargin(int depth, bool improving)
    {
        return 2000 * (depth * depth + (improving ? 2 : 1));
    }

    public static int RFPMargin(int depth)
    {
        return 1200 * depth;
    }

    public static int EfpMargin(int depth, bool pv_node)
    {
        return 1000 * (depth + (pv_node ? 1 : 0)) + 2000;
    }

    public static float HistoryMargin(int lmr_depth, bool improving)
    {
        return -(15 + 20 * lmr_depth * (lmr_depth + (improving ? 1 : 0)));
    }

    public static int MovePruning(int depth, bool improving)
    {
        int divisor = improving ? 2 : 3;

        return 6 + 18 * depth * depth / divisor;


        /*int divisor = improving ? 1 : 2;
        return 3 + 2 * depth * depth / divisor;*/
    }

    public string variation_to_string(List<int> variation)
    {
        string output = "";
        foreach (int move in variation)
            output += chessStuff.move_to_string(move) + " ";
        return output;
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

class AlphaBetaOutput
{
    public bool is_quiet;
    public bool draw;
    public List<int> movelist = new List<int>();
    public float Score;
}
class PvOut
{
    public int value = -80000;
    public List<int> principalVariation = new List<int>();
}

