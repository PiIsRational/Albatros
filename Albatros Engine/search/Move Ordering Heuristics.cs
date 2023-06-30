using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

class Move_Ordering_Heuristics
{
    Standart stuff = new Standart();
    //indexing goes [piece]
    int[] MVV_array = new int[7];
    //indexing goes [type, ply]
    int[,] killer_moves = new int[2, byte.MaxValue + 1];
    //indexing goes [color, from, to]
    float[,,] history = new float[2, 64, 64];
    //indexing goes [color, attacker, victim, victim square]
    float[,,,] chistory = new float[2, 7, 7, 64];
    //indexing goes [history type, color, piecetype previous move, to previous move][piectype current move, to current move]
    float[,,,][,] followup_and_counter_histories = new float[2, 2, 7, 64][,];
    //indexing goes [piecetype previous move, to previous move] = counter move
    int[,] counterMoves = new int[15, 64];
    public int tacticalMoveCounter = 0;
    public int[,] current_move = new int[byte.MaxValue + 1, 2];
    StandartChess chess_stuff = new StandartChess();
    public Move_Ordering_Heuristics()
    {
        initMVV();
        resetMovesort();
    }
    public void resetMovesort()
    {
        //reset counter moves
        counterMoves = new int[15, 64];

        //reset history 
        history = new float[2, 64, 64];

        //reset capture history
        chistory = new float[2, 7, 7, 64];

        //reset followup history and counter history
        followup_and_counter_histories = new float[2, 2, 7, 64][,];

        //reset killer moves
        killer_moves = new int[2, byte.MaxValue + 1];
    }
    public MoveList EvaluateMoves(Position board, int[] moves, int movelist_length, int ply, bool q_search, int tt_move, MoveList output)
    {
        float current_move_value;
        int last_move_to = int.MaxValue, move_to_follow_to = int.MaxValue;
        int last_move_piece = int.MaxValue, move_to_follow_piece = int.MaxValue;
        output.movelist = moves;
        output.eval_list = new float[movelist_length];
        tacticalMoveCounter = 0;

        if (ply - 1 >= 0)
        {
            last_move_to = current_move[ply - 1, 1];
            last_move_piece = current_move[ply - 1, 0];
            if (ply - 2 >= 0)
            {
                move_to_follow_to = current_move[ply - 2, 1];
                move_to_follow_piece = current_move[ply - 2, 0];
            }
        }

        for (int i = 0; i < movelist_length; i++) 
        {
            current_move_value = score_move(moves[i], tt_move, board, ply, last_move_to, last_move_piece, move_to_follow_to, move_to_follow_piece);

            //add the current move to the output
            output.eval_list[i] = current_move_value;
        }

        output.Length = movelist_length;

        return output;
    }
    public Movepick PickNextMove(MoveList movelist)
    {
        int best_place = 0;
        float best_score = -20000;

        for (int i = 0; i < movelist.Length; i++) 
        {
            if (movelist.eval_list[i] > best_score)
            {
                best_score = movelist.eval_list[i];
                best_place = i;
            }
        }

        Movepick output = new Movepick();
        movelist.Length--;
        output.move = movelist.movelist[best_place];
        movelist.movelist[best_place] = movelist.movelist[movelist.Length];
        output.eval = movelist.eval_list[best_place];
        movelist.eval_list[best_place] = movelist.eval_list[movelist.Length];

        return output;
    }
    public float score_move(int move, int tt_move, Position board, int ply, int last_move_to, int last_move_piece, int move_to_follow_to, int move_to_follow_piece)
    {
        float currentPieceValue = 0;

        //if transposition table move order it first
        if (move == tt_move)
        {
            tacticalMoveCounter++;
            return 40000;
        }

        //Calculate MVV value
        byte from = (byte)(move & 0b0000000000111111);
        byte to = (byte)((move & 0b0000111111000000) >> 6);
        byte other = (byte)(move >> 12);

        if (other < StandartChess.doublePawnMove)
        {
            currentPieceValue = MVV_array[board.boards[board.color ^ 1, to]];

            if (currentPieceValue != 0)
            {
                tacticalMoveCounter++;
                return 10000 + currentPieceValue + chistory[board.color, board.boards[board.color, from], board.boards[board.color ^ 1, to], to];
            }
        }
        else
        {
            switch (other)
            {
                // en passent
                case StandartChess.castle_or_en_passent:
                    if (chess_stuff.is_en_passent(move, board))
                        currentPieceValue = MVV_array[StandartChess.pawn];
                    break;
                // Knight promotion
                case StandartChess.knight_promotion:
                    currentPieceValue = MVV_array[StandartChess.knight];
                    break;
                // Bishop promotion
                case StandartChess.bishop_promotion:
                    currentPieceValue = MVV_array[StandartChess.bishop];
                    break;
                // Queen promotion
                case StandartChess.queen_promotion:
                    currentPieceValue = MVV_array[StandartChess.queen];
                    break;
                // Rook promotion
                case StandartChess.rook_promotion:
                    currentPieceValue = MVV_array[StandartChess.rook];
                    break;
            }

            if (currentPieceValue != 0)
            {
                tacticalMoveCounter++;
                return 10000 + currentPieceValue;
            }
        }

        //killer moves
        if (killer_moves[0, ply] == move + 1)
            return 6000;
        if (killer_moves[1, ply] == move + 1)
            return 5000;

        //counter moves
        if (last_move_to != int.MaxValue && counterMoves[last_move_piece, last_move_to] == move + 1)
            return 4000;

        /*history moves*/

        //normal history
        return history[board.color, from, to]/* +
            //counter history
            (ply - 1 < 0 || followup_and_counter_histories[board.color, 0, last_move_piece, last_move_to] == null ? 0 :
            followup_and_counter_histories[board.color, 0, last_move_piece, last_move_to][board.boards[board.color, from], to]) +
            //followup history
            (ply - 2 < 0 || followup_and_counter_histories[board.color, 1, move_to_follow_piece, move_to_follow_to] == null ? 0 :
            followup_and_counter_histories[board.color, 1, move_to_follow_piece, move_to_follow_to][board.boards[board.color, from], to])*/;
    }
    public void update_counter_moves(Position board, int last_move_to, int last_move_piece, int move, int ply, bool[] null_move_pruning)
    {
        if (!null_move_pruning[ply - 1] && last_move_to != int.MaxValue)
            counterMoves[last_move_piece, last_move_to] = move + 1;
    }
    public void add_current_move(int move, Position board, int ply)
    {
        byte from = (byte)(move & 0b0000000000111111);
        byte to = (byte)((move & 0b0000111111000000) >> 6);

        current_move[ply, 0] = board.boards[board.color, from];
        current_move[ply, 1] = to;
    }
    public void update_killer_moves(int move, int ply)
    {
        if (move == killer_moves[0, ply])
        {
            if (killer_moves[0, ply] != 0)
                killer_moves[1, ply] = killer_moves[0, ply];

            killer_moves[0, ply] = move + 1;
        }
    }
    public unsafe void update_history_move(Position board, int move, int last_move_to, int last_move_piece, int move_to_follow_to, int move_to_follow_piece, float bonus, int ply)
    {
        byte from = (byte)(move & 0b0000000000111111);
        byte to = (byte)((move & 0b0000111111000000) >> 6);
        //normal history
        fixed (float* place = &history[board.color, from, to])
        {
            *place = history_score_update(*place, bonus);
        }

        //counter history
        if (ply - 1 >= 0)
        {
            if (followup_and_counter_histories[board.color, 0, last_move_piece, last_move_to] == null)
                followup_and_counter_histories[board.color, 0, last_move_piece, last_move_to] = new float[7, 64];

            followup_and_counter_histories[board.color, 0, last_move_piece, last_move_to][board.boards[board.color, from], to] =
                history_score_update(followup_and_counter_histories[board.color, 0, last_move_piece, last_move_to][board.boards[board.color, from], to], bonus);

            //followup history
            if (ply - 2 >= 0)
            {
                if (followup_and_counter_histories[board.color, 1, move_to_follow_piece, move_to_follow_to] == null)
                    followup_and_counter_histories[board.color, 1, move_to_follow_piece, move_to_follow_to] = new float[7, 64];

                followup_and_counter_histories[board.color, 1, move_to_follow_piece, move_to_follow_to][board.boards[board.color, from], to] =
                    history_score_update(followup_and_counter_histories[board.color, 1, move_to_follow_piece, move_to_follow_to][board.boards[board.color, from], to], bonus);
            }
        }
    }
    public void UpdateHistories(Position board, int bestmove, int[] played_moves, int current_move_idx, bool[] null_move_pruning, int depth, int ply, bool fail_high)
    {
        float bonus = -Math.Min((float)(depth * depth) / 10, 40);

        if (fail_high && !chess_stuff.is_capture(bestmove, board))
        {
            //update the killer moves
            update_killer_moves(bestmove, ply);

            //update counter moves
            if (ply - 1 >= 0) update_counter_moves(board, current_move[ply - 1, 1], current_move[ply - 1, 0], bestmove, ply, null_move_pruning);
        }

        for (int i = 0; i < current_move_idx; i++) 
        {
            if (played_moves[i] == bestmove)
                bonus = -bonus;

            if (!chess_stuff.is_capture(played_moves[i], board))
                update_history_move(board, played_moves[i], ply - 1 >= 0 ? current_move[ply - 1, 1] : 0, ply - 1 >= 0 ? current_move[ply - 1, 0] : 0,
                                                            ply - 2 >= 0 ? current_move[ply - 2, 1] : 0, ply - 2 >= 0 ? current_move[ply - 2, 0] : 0, bonus, ply);
            else
                update_chistory_move(board, played_moves[i], bonus);
        }
    }
    public unsafe void update_chistory_move(Position board, int move, float bonus)
    {
        byte from = (byte)(move & 0b0000000000111111);
        byte to = (byte)((move & 0b0000111111000000) >> 6);

        fixed (float* place = &chistory[board.color, board.boards[board.color, from], board.boards[board.color ^ 1, to], to])
            *place = history_score_update(*place, bonus);
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

        for (int victim = 0; victim < 7; victim++) 
        {
            victim_value = get_MVV_piece_value(victim);

            MVV_array[victim] = victim_value;     
        }
    }
    public int get_MVV_piece_value(int piece)
    {
        int piece_value = 0;

        switch (piece)
        {
            case StandartChess.pawn:
                piece_value = 3000;
                break;
            case StandartChess.knight:
                piece_value = 9000;
                break;
            case StandartChess.bishop:
                piece_value = 9000;
                break;
            case StandartChess.queen:
                piece_value = 27000;
                break;
            case StandartChess.rook:
                piece_value = 15000;
                break;
            default:
                piece_value = 0;
                break;
        }
        return piece_value;
    }
}
class MoveList
{
    public int[] movelist;
    public float[] eval_list;
    public int Length;
}
class Movepick
{
    public int move;
    public float eval;
}

