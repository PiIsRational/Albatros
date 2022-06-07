using System;
using System.Collections.Generic;
using System.Text;

class Move_Ordering_Heuristics
{
    standart stuff = new standart();
    //indexing goes [piece]
    int[] MVV_array = new int[11];
    //indexing goes [type, ply]
    int[,][] killer_moves = new int[2, byte.MaxValue + 1][];
    //indexing goes [color, from, to]
    float[,,,,] history = new float[2, 9, 9, 9, 9];
    //indexing goes [color, attacker, victim, victim square]
    float[,,,,] chistory = new float[2, 11, 11, 9, 9];
    //indexing goes [history type, color, piecetype previous move, to previous move][piectype current move, to current move]
    float[,,,,][,,] followup_and_counter_histories = new float[2, 2, 11, 9, 9][,,];
    //indexing goes [piecetype previous move, to previous move][counter move]
    int[,,][] counter_moves = new int[27, 9, 9][];
    public int tactical_move_counter = 0;
    public int[][] current_move = new int[byte.MaxValue + 1][];
    standart_chess chess_stuff = new standart_chess();
    public Move_Ordering_Heuristics()
    {
        for (int i = 0; i < current_move.Length; i++)
            current_move[i] = new int[5];

        initMVV();
        reset_movesort();
    }
    public void reset_movesort()
    {
        //reset counter moves
        counter_moves = new int[27, 9, 9][];

        //reset history 
        history = new float[2, 9, 9, 9, 9];

        //reset capture history
        chistory = new float[2, 11, 11, 9, 9];

        //reset followup history and counter history
        followup_and_counter_histories = new float[2, 2, 11, 9, 9][,,];

        //reset killer moves
        killer_moves = new int[2, byte.MaxValue + 1][];
    }
    public move_and_eval_list evaluate_moves(byte[,] board, List<int[]> moves, int ply, byte color, bool q_search ,int[] tt_move)
    {
        move_and_eval_list output = new move_and_eval_list();
        output.movelist = moves;
        output.eval_list = new List<float>();
        float current_move_value = 0;
        int[] last_move = null, move_to_follow = null;
        List<float> Values = new List<float>();
        List<int[]> SortedMoves = new List<int[]>();
        tactical_move_counter = 0;

        if (ply - 1 >= 0)
        {
            last_move = current_move[ply - 1];

            if (ply - 2 >= 0)
                move_to_follow = current_move[ply - 2];
        }

        foreach (int[] move in moves)
        {
            current_move_value = score_move(move, tt_move, board, ply, last_move, move_to_follow, color);

            //add the current move to the output
            output.eval_list.Add(current_move_value);
        }

        return output;
    }
    public movepick pick_next_move(move_and_eval_list movelist)
    {
        int best_place = 0, movecount = 0;
        float best_score = -20000;

        foreach(float score in movelist.eval_list)
        {
            if(score > best_score)
            {
                best_score = score;
                best_place = movecount;
            }

            movecount++;
        }

        movepick output = new movepick();
        output.move = movelist.movelist[best_place];
        movelist.movelist.RemoveAt(best_place);
        output.eval = movelist.eval_list[best_place];
        movelist.eval_list.RemoveAt(best_place);

        return output;
    }
    public float score_move(int[] move, int[] tt_move, byte[,] board, int ply, int[] last_move, int[] move_to_follow, byte color)
    {
        float current_piece_value = 0;

        //if transposition table move order it first
        if (stuff.int_array_equal(move, tt_move))
        {
            tactical_move_counter++;
            return 40000;
        }

        //Calculate MVV value
        if (move.Length == 4)
        {
            current_piece_value = MVV_array[board[move[2], move[3]] & 0b00001111];

            if (current_piece_value != 0)
            {
                tactical_move_counter++;
                return 10000 + current_piece_value + chistory[color, board[move[0], move[1]] & 0b00001111, board[move[2], move[3]] & 0b00001111, move[2], move[3]];
            }
        }
        else
        {
            switch (move[4])
            {
                // en passent
                case 0:
                    if (chess_stuff.is_tactical(move, board))
                        current_piece_value = MVV_array[1];
                    break;
                // Knight promotion
                case 1:
                    current_piece_value = MVV_array[4];
                    break;
                // Bishop promotion
                case 2:
                    current_piece_value = MVV_array[5];
                    break;
                // Queen promotion
                case 3:
                    current_piece_value = MVV_array[8];
                    break;
                // Rook promotion
                case 4:
                    current_piece_value = MVV_array[10];
                    break;
            }

            tactical_move_counter++;
            return 10000 + current_piece_value + chistory[color, board[move[0], move[1]] & 0b00001111, board[move[2], move[3]] & 0b00001111, move[2], move[3]];
        }

        //killer moves
        if (stuff.int_array_equal(killer_moves[0, ply], move))
            return 6000;
        if (stuff.int_array_equal(killer_moves[1, ply], move))
            return 5000;

        //counter moves
        if (last_move != null && stuff.int_array_equal(counter_moves[last_move[4], last_move[2], last_move[3]], move))
            return 4000;

        /*history moves*/

        //normal history
        return history[color, move[0], move[1], move[2], move[3]] +
            //counter history
            (ply - 1 < 0 || followup_and_counter_histories[color, 0, last_move[4], last_move[2], last_move[3]] == null ? 0 :
            followup_and_counter_histories[color, 0, last_move[4], last_move[2], last_move[3]][board[move[0], move[1]] & 0b00001111, move[2], move[3]]) +
            //followup history
            (ply - 2 < 0 || followup_and_counter_histories[color, 1, move_to_follow[4], move_to_follow[2], move_to_follow[3]] == null ? 0 :
            followup_and_counter_histories[color, 1, move_to_follow[4], move_to_follow[2], move_to_follow[3]][board[move[0], move[1]] & 0b00001111, move[2], move[3]]);
    }
    public void update_counter_moves(byte[,] board, int[] last_move, int[] move, int ply, bool[] null_move_pruning, byte color)
    {
        if (!null_move_pruning[ply - 1] && last_move.Length != 0)
            counter_moves[last_move[4], last_move[2], last_move[3]] = move;
    }
    public void add_current_move(int[] move , byte[,] board , int ply)
    {
        Array.Copy(move, current_move[ply], 4);
        current_move[ply][4] = board[move[0], move[1]] & 0b00001111;
    }
    public void update_killer_moves(int[] move, int ply)
    {
        if (!stuff.int_array_equal(move, killer_moves[0, ply]))
        {
            if (killer_moves[0, ply] != null)
            {
                killer_moves[1, ply] = new int[killer_moves[0, ply].Length];
                Array.Copy(killer_moves[0, ply], killer_moves[1, ply], killer_moves[0, ply].Length);
            }

            killer_moves[0, ply] = move;
        }
    }
    public void update_history_move(byte[,] board, int[] move, int[] last_move, int[] move_to_follow, float bonus, byte color, int ply)
    {
        //normal history 
        history[color, move[0], move[1], move[2], move[3]] = history_score_update(history[color, move[0], move[1], move[2], move[3]], bonus);

        //counter history
        if (ply - 1 >= 0)
        {
            if (followup_and_counter_histories[color, 0, last_move[4], last_move[2], last_move[3]] == null)
                followup_and_counter_histories[color, 0, last_move[4], last_move[2], last_move[3]] = new float[11, 9, 9];

            followup_and_counter_histories[color, 0, last_move[4], last_move[2], last_move[3]][board[move[0], move[1]] & 0b00001111, move[2], move[3]] =
                history_score_update(followup_and_counter_histories[color, 0, last_move[4], last_move[2], last_move[3]][board[move[0], move[1]] & 0b00001111, move[2], move[3]], bonus);

            //followup history
            if (ply - 2 >= 0)
            {
                if (followup_and_counter_histories[color, 1, move_to_follow[4], move_to_follow[2], move_to_follow[3]] == null)
                    followup_and_counter_histories[color, 1, move_to_follow[4], move_to_follow[2], move_to_follow[3]] = new float[11, 9, 9];

                followup_and_counter_histories[color, 1, move_to_follow[4], move_to_follow[2], move_to_follow[3]][board[move[0], move[1]] & 0b00001111, move[2], move[3]] =
                    history_score_update(followup_and_counter_histories[color, 1, move_to_follow[4], move_to_follow[2], move_to_follow[3]][board[move[0], move[1]] & 0b00001111, move[2], move[3]], bonus);
            }
        }
    }
    public void update_histories(byte[,] board, int[] bestmove, List<int[]> played_moves, bool[] null_move_pruning, byte color, int depth, int ply)
    {
        float bonus = -Math.Min((float)(depth * depth) / 10, 40);

        if (!chess_stuff.is_capture(bestmove , board))
        {
            //update the killer moves
            update_killer_moves(bestmove, ply);

            //update counter moves
            update_counter_moves(board, ply - 1 >= 0 ? current_move[ply - 1] : new int[0], bestmove, ply, null_move_pruning, color);
        }

        foreach (int[] move in played_moves)
        {
            if (stuff.int_array_equal(move, bestmove))
                bonus = -bonus;

            if (!chess_stuff.is_capture(move, board))
                update_history_move(board, move, ply - 1 >= 0 ? current_move[ply - 1] : new int[0], ply - 2 >= 0 ? current_move[ply - 2] : new int[0], bonus, color, ply);
            else
                update_chistory_move(board, move, color, bonus);
        }
    }
    public void update_chistory_move(byte[,] board, int[] move, byte color, float bonus)
    {
        chistory[color, board[move[0], move[1]] & 0b00001111, board[move[2], move[3]] & 0b00001111, move[2], move[3]] =
            history_score_update(chistory[color, board[move[0], move[1]] & 0b00001111, board[move[2], move[3]] & 0b00001111, move[2], move[3]], bonus);
    }
    public float history_score_update_(float current_score, float margin)
    {
        float margin_sign = margin > 0 ? 1 : -1, score_max_delta = Math.Min(1000 - margin_sign * current_score * 1f, 4), max_margin = 4;

        return current_score * 1f + margin * (score_max_delta / max_margin);
    }
    public float history_score_update(float current_score, float margin)
    {
        return Math.Min(Math.Max(current_score + margin - current_score * Math.Abs(margin) / 10000, -1000), 1000);
    }
    public void initMVV()
    {
        int victim_value = 0;

        for (int victim = 0; victim < 11; victim++) 
        {
            victim_value = get_MVV_piece_value(victim);

            MVV_array[victim] = victim_value;     
        }
    }
    public int get_MVV_piece_value(int piece)
    {
        int piece_value = 0;

        switch (piece & 0b00001111)
        {
            case 0b00000001:
                piece_value = 3000;
                break;
            case 0b00000010:
                piece_value = 3000;
                break;
            case 0b00000011:
                piece_value = 3000;
                break;
            case 0b00000100:
                piece_value = 6000;
                break;
            case 0b00000101:
                piece_value = 9000;
                break;
            case 0b00001000:
                piece_value = 15000;
                break;
            case 0b00001001:
                piece_value = 12000;
                break;
            case 0b00001010:
                piece_value = 12000;
                break;
            case 0b00000110:
                piece_value = 18000;
                break;
            case 0b00000111:
                piece_value = 18000;
                break;
            default:
                piece_value = 0;
                break;
        }
        return piece_value;
    }
}
class move_and_eval_list
{
    public List<int[]> movelist;
    public List<float> eval_list;
}
class movepick
{
    public int[] move;
    public float eval;
}

