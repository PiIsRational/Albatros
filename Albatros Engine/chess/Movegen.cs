using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

class movegen
{
    /*
     * a move can use up to 15-bits and is stored in an integer
     * as an example move m = 001 000000 111111
     * the last 6 bits give  the starting square of the move
     * the next 6 bits give the ending square of the move
     * and the first 3 bits tell if the move is special (promotion, en passent, castling)
     */
    //move on the board
    const int inx = 1;
    const int iny = 8;

    //piece move lookup tables
    byte[][] knight_table = new byte[64][];
    byte[][] king_table = new byte[64][];
    byte[,][] bishop_table = new byte[64, 4][];
    byte[,][] rook_table = new byte[64, 4][];

    //variables
    standart_chess chess_stuff = new standart_chess();
    int[,] start_rook_square = new int[2, 2] { { 56, 63 }, { 0, 7 } };
    public bool illegal_position = false;
    public bool non_pawn_material = false;
    bool reset_fifty_move_counter = false;
    public int move_idx = 0;
    public movegen()
    {
        init_knight_move();
        init_bishop_move();
        init_rook_move();
        init_king_move();
    }
    public int is_mate(position board, bool in_check, reverse_move undo_move, int[] movelist)
    {
        //generate pseudolegal_movelist
        movelist = generate_movelist(board, movelist);

        //loop through each move
        for (int i = 0; i < move_idx; i++)
        {
            if (illegality_check_neccessaray(movelist[i], in_check, board))
            {
                //make move on the board
                board = make_move(board, movelist[i], true, undo_move);

                //look if the position is illegal
                if (check(board, true))
                {
                    move_idx--;
                    movelist[i] = movelist[move_idx];
                    i--;
                }
                else
                {
                    //make the reverse move
                    board = unmake_move(board, undo_move);

                    return 2;
                }

                //make the reverse move
                board = unmake_move(board, undo_move);
            }
            else
                return 2;
        }


        //checkmate
        if (check(board, false))
            return -1;
        else
            return 0;
    }
    public int[] generate_movelist(position board, int[] movelist)
    {
        move_idx = 0;
        non_pawn_material = false;
        //loop through each piece with the current color
        for (byte i = 0; i < 64; i++)
        {
            if (board.boards[board.color, i] != 0)
            {
                byte piece_wo_color = board.boards[board.color, i];

                switch (piece_wo_color)
                {
                    case standart_chess.pawn:
                        movelist = pawn_move(movelist, board, i);
                        break;
                    case standart_chess.knight:
                        non_pawn_material = true;
                        movelist = knight_move(movelist, board, i);
                        break;
                    case standart_chess.bishop:
                        non_pawn_material = true;
                        movelist = sliding_piece_move(movelist, board, i, bishop_table);
                        break;
                    case standart_chess.rook:
                        non_pawn_material = true;
                        movelist = sliding_piece_move(movelist, board, i, rook_table);
                        break;
                    case standart_chess.queen:
                        non_pawn_material = true;
                        movelist = sliding_piece_move(sliding_piece_move(movelist, board, i, bishop_table), board, i, rook_table);
                        break;
                    case standart_chess.king:
                        non_pawn_material = true;
                        movelist = king_move(movelist, board, i);
                        break;
                }
            }
        }
        return movelist;
    }
    public int[] generate_capture_list(position board, int[] movelist)
    {
        move_idx = 0;
        //loop through each piece with the current color
        for (byte i = 0; i < 64; i++)
        {
            if (board.boards[board.color, i] != 0)
            {
                byte piece_wo_color = board.boards[board.color, i];

                switch (piece_wo_color)
                {
                    case standart_chess.pawn:
                        movelist = pawn_capture(movelist, board, i);
                        break;
                    case standart_chess.knight:
                        movelist = knight_capture(movelist, board, i);
                        break;
                    case standart_chess.bishop:
                        movelist = sliding_piece_capture(movelist, board, i, bishop_table);
                        break;
                    case standart_chess.rook:
                        movelist = sliding_piece_capture(movelist, board, i, rook_table);
                        break;
                    case standart_chess.queen:
                        movelist = sliding_piece_capture(sliding_piece_capture(movelist, board, i, bishop_table), board, i, rook_table);
                        break;
                    case standart_chess.king:
                        movelist = king_capture(movelist, board, i);
                        break;
                }
            }
        }
        return movelist;
    }
    public int[] legal_move_generator(position board, bool in_check, reverse_move undo_move, int[] movelist)
    {
        //generate pseudolegal_movelist
        int[] first_movelist = generate_movelist(board, movelist);
        
        //loop through each move
        for (int i = 0; i < move_idx; i++)
        {
            if (illegality_check_neccessaray(first_movelist[i], in_check, board))
            {
                //make move on the board
                board = make_move(board, first_movelist[i], true, undo_move);

                //look if the position is illegal
                if (check(board, true))
                {
                    move_idx--;
                    first_movelist[i] = first_movelist[move_idx];
                    i--;
                }

                //make the reverse move
                board = unmake_move(board, undo_move);
            }
        }
        return first_movelist;
    }
    public int[] legal_capture_generator(position board, bool in_check, reverse_move undo_move, int[] movelist)
    {
        //generate pseudolegal_movelist
        int[] first_movelist = generate_capture_list(board, movelist);

        //loop through each move
        for (int i = 0; i < move_idx; i++)
        {
            if (illegality_check_neccessaray(first_movelist[i], in_check, board))
            {
                //make move on the board
                board = make_move(board, first_movelist[i], true, undo_move);

                //look if the position is illegal
                if (check(board, true))
                {
                    move_idx--;
                    first_movelist[i] = first_movelist[move_idx];
                    i--;
                }

                //make the reverse move
                board = unmake_move(board, undo_move);
            }
        }

        return first_movelist;
    }
    public bool fast_check(position board, int move)
    {
        byte other = (byte)(move >> 12);
        byte from = (byte)(move & 0b0000000000111111);
        byte to = (byte)((move & 0b0000111111000000) >> 6);

        int king_square = board.piece_square_lists[standart_chess.king ^ (board.color << 3)][0];
        int othercolor = board.color ^ 1;
        int displaced_other_color = othercolor << 3;
        int king_x = chess_stuff.get_x_from_square((byte)king_square);
        int king_y = chess_stuff.get_y_from_square((byte)king_square);

        //only special cases
        if (other == standart_chess.castle_or_en_passent)
        {
            if(board.boards[othercolor, to] == standart_chess.king )
            { 
                //find the square on wich the rook landed
                byte rook_square = (byte)(from + ((from < to) ? 1 : -1));

                int rook_x = chess_stuff.get_x_from_square((byte)rook_square);
                //calculate the first delta
                if (king_x == rook_x)
                    check_vector_for_check(board, (byte)king_square, rook_table, king_square - rook_square < 0 ? 2 : 3, (byte)othercolor, standart_chess.rook);
                else if (king_square - rook_square == king_x - rook_x)
                    check_vector_for_check(board, (byte)king_square, rook_table, king_x - rook_x < 0 ? 0 : 1, (byte)othercolor, standart_chess.rook);

                if (illegal_position)
                {
                    illegal_position = false;
                    return true;
                }

                return false;
            }
            else
            {
                int y_dir = (int)((1 - (othercolor << 1)) << 3);
                byte en_passent_square = (byte)(to + y_dir);
                byte ep_x = chess_stuff.get_x_from_square(en_passent_square);

                from_check(board, (byte)king_square, (byte)king_x, en_passent_square, ep_x, (byte)othercolor);
            }
        }

        //the rest is normal

        //cases at the start square
        int from_x = chess_stuff.get_x_from_square(from);
        from_check(board, (byte)king_square, (byte)king_x, from, (byte)from_x, (byte)othercolor);

        int to_x = chess_stuff.get_x_from_square(to);

        //cases at the end square
        switch (board.boards[othercolor, to])
        {
            case standart_chess.queen:
                //calculate the first delta
                if (king_x == to_x)
                    check_vector_for_check(board, (byte)king_square, rook_table, king_square - to < 0 ? 2 : 3, (byte)othercolor, standart_chess.queen);
                else
                {
                    int delta_a = king_square - to;
                    int delta_b = king_x - to_x;

                    if (delta_a == delta_b)
                        check_vector_for_check(board, (byte)king_square, rook_table, king_x - to_x < 0 ? 0 : 1, (byte)othercolor, standart_chess.queen);
                    else if (delta_a == 9 * delta_b)
                        check_vector_for_check(board, (byte)king_square, bishop_table, king_x - to_x < 0 ? 0 : 1, (byte)othercolor, standart_chess.queen);
                    else if (delta_a == -7 * delta_b)
                        check_vector_for_check(board, (byte)king_square, bishop_table, king_x - to_x < 0 ? 3 : 2, (byte)othercolor, standart_chess.queen);
                }
                break;
            case standart_chess.rook:
                //calculate the first delta
                if (king_x == to_x)
                    check_vector_for_check(board, (byte)king_square, rook_table, king_square - to < 0 ? 2 : 3, (byte)othercolor, standart_chess.rook);
                else if (king_square - to == king_x - to_x)
                    check_vector_for_check(board, (byte)king_square, rook_table, king_x - to_x < 0 ? 0 : 1, (byte)othercolor, standart_chess.rook);
                break;
            case standart_chess.bishop:
                //calculate the first delta
                if (king_x != to_x)
                {
                    int delta_a = king_square - to;
                    int delta_b = king_x - to_x;

                    if (delta_a == 9 * delta_b)
                        check_vector_for_check(board, (byte)king_square, bishop_table, king_x - to_x < 0 ? 0 : 1, (byte)othercolor, standart_chess.bishop);
                    if (delta_a == -7 * delta_b)
                        check_vector_for_check(board, (byte)king_square, bishop_table, king_x - to_x < 0 ? 3 : 2, (byte)othercolor, standart_chess.bishop);
                }
                break;
            case standart_chess.knight:
                int delta_x = Math.Abs((int)king_x - to_x);
                if (delta_x == 1 || delta_x == 2)
                {
                    int to_y = chess_stuff.get_y_from_square(to);
                    int delta_y = Math.Abs((int)king_y - to_y);
                    if (delta_y == 3 - delta_x)
                        return true;
                }
                break;
            case standart_chess.pawn:
                int y_dir = (1 - 2 * othercolor) * iny;
                if (king_square + y_dir < 64 && king_square + y_dir >= 0)
                {
                    if (king_x - 1 >= 0 && board.boards[othercolor, king_square - 1 + y_dir] == standart_chess.pawn)
                        return true;

                    if (king_x + 1 < 8 && board.boards[othercolor, king_square + 1 + y_dir] == standart_chess.pawn)
                        return true;
                }
                break;
        }

        if (illegal_position)
        {
            illegal_position = false;
            return true;
        }

        return false;
    }
    public void from_check(position board,byte king_square, byte king_x, byte from, byte from_x, byte othercolor)
    {
        if (king_x == from_x)
        {
            check_vector_for_check(board, (byte)king_square, rook_table, king_square - from < 0 ? 2 : 3, (byte)othercolor, standart_chess.queen);
            check_vector_for_check(board, (byte)king_square, rook_table, king_square - from < 0 ? 2 : 3, (byte)othercolor, standart_chess.rook);
        }
        else
        {
            int delta_a = king_square - from;
            int delta_b = king_x - from_x;

            if (delta_a == delta_b)
            {
                check_vector_for_check(board, (byte)king_square, rook_table, king_x - from_x < 0 ? 0 : 1, (byte)othercolor, standart_chess.rook);
                check_vector_for_check(board, (byte)king_square, rook_table, king_x - from_x < 0 ? 0 : 1, (byte)othercolor, standart_chess.queen);
            }
            else if (delta_a == 9 * delta_b)
            {
                check_vector_for_check(board, (byte)king_square, bishop_table, king_x - from_x < 0 ? 0 : 1, (byte)othercolor, standart_chess.bishop);
                check_vector_for_check(board, (byte)king_square, bishop_table, king_x - from_x < 0 ? 0 : 1, (byte)othercolor, standart_chess.queen);
            }
            else if (delta_a == -7 * delta_b)
            {
                check_vector_for_check(board, (byte)king_square, bishop_table, king_x - from_x < 0 ? 3 : 2, (byte)othercolor, standart_chess.bishop);
                check_vector_for_check(board, (byte)king_square, bishop_table, king_x - from_x < 0 ? 3 : 2, (byte)othercolor, standart_chess.queen);
            }
        }
    }
    public bool illegality_check_neccessaray(int move, bool in_check, position board)
    {
        int from = move & 0b0000000000111111;
        if (in_check || board.boards[board.color, from] == standart_chess.king)
            return true;

        int from_x = chess_stuff.get_x_from_square((byte)from);
        int king_square = board.piece_square_lists[standart_chess.king ^ (board.color << 3)][0];
        int king_x = chess_stuff.get_x_from_square((byte)king_square);

        if (from_x == king_x)
            return true;

        int delta_a = king_square - from;
        int delta_b = king_x - from_x;

        if (delta_a == delta_b || delta_a == 9 * delta_b || delta_a == -7 * delta_b)
            return true;


        return false;
    }
    public bool check(position board, bool illegality_check)
    {
        //find king 
        byte othercolor = illegality_check ? board.color : (byte)(board.color ^ 1);
        int displaced_other_color = othercolor << 3;
        int king_square = board.piece_square_lists[standart_chess.king ^ ((othercolor ^ 1) << 3)][0];
        int king_x = chess_stuff.get_x_from_square((byte)king_square);
        int king_y = chess_stuff.get_y_from_square((byte)king_square);

        //the goal is to ckeck if the king is on the same diagonal as the piece
        //if this is the case just check the diagonal

        //queen
        for (int i = 0; i < board.piececount[standart_chess.queen ^ displaced_other_color]; i++)
        {
            byte queen_square = board.piece_square_lists[standart_chess.queen ^ displaced_other_color][i];
            int queen_x = chess_stuff.get_x_from_square((byte)queen_square);
            //calculate the first delta
            if (king_x == queen_x)
                check_vector_for_check(board, (byte)king_square, rook_table, king_square - queen_square < 0 ? 2 : 3, othercolor, standart_chess.queen);
            else
            {
                int delta_a = king_square - queen_square;
                int delta_b = king_x - queen_x;

                if (delta_a == delta_b)
                    check_vector_for_check(board, (byte)king_square, rook_table, king_x - queen_x < 0 ? 0 : 1, othercolor, standart_chess.queen);
                else if (delta_a == 9 * delta_b)
                    check_vector_for_check(board, (byte)king_square, bishop_table, king_x - queen_x < 0 ? 0 : 1, othercolor, standart_chess.queen);
                else if (delta_a == -7 * delta_b)
                    check_vector_for_check(board, (byte)king_square, bishop_table, king_x - queen_x < 0 ? 3 : 2, othercolor, standart_chess.queen);
            }
        }

        //rook
        for (int i = 0; i < board.piececount[standart_chess.rook ^ displaced_other_color]; i++)
        {
            byte rook_square = board.piece_square_lists[standart_chess.rook ^ displaced_other_color][i];
            int rook_x = chess_stuff.get_x_from_square((byte)rook_square);
            //calculate the first delta
            if (king_x == rook_x)
                check_vector_for_check(board, (byte)king_square, rook_table, king_square - rook_square < 0 ? 2 : 3, othercolor, standart_chess.rook);
            else if (king_square - rook_square == king_x - rook_x)
                check_vector_for_check(board, (byte)king_square, rook_table, king_x - rook_x < 0 ? 0 : 1, othercolor, standart_chess.rook);
        }

        //bishop
        for (int i = 0; i < board.piececount[standart_chess.bishop ^ displaced_other_color]; i++)
        {
            byte bishop_square = board.piece_square_lists[standart_chess.bishop ^ displaced_other_color][i];
            int bishop_x = chess_stuff.get_x_from_square((byte)bishop_square);
            //calculate the first delta
            if (king_x != bishop_x)
            {
                int delta_a = king_square - bishop_square;
                int delta_b = king_x - bishop_x;

                if (delta_a == 9 * delta_b)
                    check_vector_for_check(board, (byte)king_square, bishop_table, king_x - bishop_x < 0 ? 0 : 1, othercolor, standart_chess.bishop);
                if (delta_a == -7 * delta_b)
                    check_vector_for_check(board, (byte)king_square, bishop_table, king_x - bishop_x < 0 ? 3 : 2, othercolor, standart_chess.bishop);
            }
        }

        if (illegal_position)
        {
            illegal_position = false;
            return true;
        }

        //knight
        for (int i = 0; i < board.piececount[standart_chess.knight ^ displaced_other_color]; i++)
        {
            byte knight_square = board.piece_square_lists[standart_chess.knight ^ displaced_other_color][i];
            int knight_x = chess_stuff.get_x_from_square(knight_square);
            int delta_x = Math.Abs((int)king_x - knight_x);
            if (delta_x == 1 || delta_x == 2)
            {
                int knight_y = chess_stuff.get_y_from_square(knight_square);
                int delta_y = Math.Abs((int)king_y - knight_y);
                if (delta_y == 3 - delta_x)
                    return true;
            }
        }

        //pawns
        int y_dir = (1 - 2 * othercolor) * iny;
        if (king_square + y_dir < 64 && king_square + y_dir >= 0)
        {
            if (king_x - 1 >= 0 && board.boards[othercolor, king_square - 1 + y_dir] == standart_chess.pawn)
                return true;

            if (king_x + 1 < 8 && board.boards[othercolor, king_square + 1 + y_dir] == standart_chess.pawn)
                return true;
        }

        //king
        if (illegality_check)
        {
            int other_king_square = board.piece_square_lists[standart_chess.king ^ displaced_other_color][0];
            int other_k_x = chess_stuff.get_x_from_square((byte)other_king_square);
            if (other_k_x == king_x + 1 || other_k_x == king_x || other_k_x == king_x - 1)
            {
                int other_k_y = chess_stuff.get_y_from_square((byte)other_king_square);
                if (other_k_y == king_y + 1 || other_k_y == king_y || other_k_y == king_y - 1)
                    return true;
            }
        }
        return false;
    }
    public position make_move(position board, int move, bool generate_reverse_move, reverse_move undo_move)
    {
        byte other = (byte)(move >> 12);
        byte from = (byte)(move & 0b0000000000111111);
        byte to = (byte)((move & 0b0000111111000000) >> 6);
        byte piece = board.boards[board.color, from];
        bool ep_legal = false;

        if (generate_reverse_move)
        {
            undo_move.rook_changes = byte.MaxValue;
            undo_move.king_changes = byte.MaxValue;
            undo_move.moved_piece_idx = 0;
            undo_move.removed_piece_idx = 0;
            undo_move.fifty_move_rule = (byte)board.fifty_move_rule;
            undo_move.en_passent = board.en_passent_square;
        }

        //look for en passent
        if (board.en_passent_square != standart_chess.no_square && other != standart_chess.double_pawn_move)
            ep_legal = true;

        //reset rook has moved for castling
        if (piece == standart_chess.rook || board.boards[board.color ^ 1, to] == standart_chess.rook)
        {
            byte rook_color, rook_square;
            if (piece == standart_chess.rook)
            {
                rook_color = board.color;
                rook_square = from;
                board = set_rook_moved(board, rook_square, rook_color, generate_reverse_move, undo_move);
            }

            if (board.boards[board.color ^ 1, to] == standart_chess.rook)
            {
                rook_color = (byte)(board.color ^ 1);
                rook_square = to;
                board = set_rook_moved(board, rook_square, rook_color, generate_reverse_move, undo_move);
            }
        }

        switch (piece)
        {
            case standart_chess.pawn:
                board.fifty_move_rule = 0;
                board = pawn_execute_move(board, from, to, other, generate_reverse_move, undo_move);
                break;
            case standart_chess.king:
                board.fifty_move_rule++;
                board = king_execute_move(board, from, to, other, generate_reverse_move, undo_move);
                break;
            default:
                board.fifty_move_rule++;
                board = normal_piece_execute_move(board, from, to, generate_reverse_move, undo_move);
                break;
        }

        if (board.en_passent_square != standart_chess.no_square && ep_legal)
            board.en_passent_square = standart_chess.no_square;

        if (reset_fifty_move_counter)
        {
            board.fifty_move_rule = 0;
            reset_fifty_move_counter = false;
        }

        board.color = (byte)(board.color ^ 1);

        return board;
    }
    public position set_rook_moved(position board, byte rook_square, byte rook_color, bool reverse_move_update, reverse_move undo_move)
    {
        if (reverse_move_update && undo_move.rook_changes == 255)
            undo_move.rook_changes = 0;

        if (start_rook_square[rook_color, 0] == rook_square && board.rook_not_moved[(rook_color << 1) ^ 0])
        {
            if (reverse_move_update) undo_move.rook_changes ^= (byte)(1 << ((rook_color << 1) ^ 0));
            board.rook_not_moved[(rook_color << 1) ^ 0] = false;
        }
        else if (start_rook_square[rook_color, 1] == rook_square && board.rook_not_moved[(rook_color << 1) ^ 1])
        {
            if (reverse_move_update) undo_move.rook_changes ^= (byte)(1 << ((rook_color << 1) ^ 1));
            board.rook_not_moved[(rook_color << 1) ^ 1] = false;
        }

        return board;
    }
    public position unmake_move(position board, reverse_move move)
    {
        board.color = (byte)(board.color ^ 1);
        board.en_passent_square = move.en_passent;
        board.fifty_move_rule = move.fifty_move_rule;
        if (move.king_changes != byte.MaxValue)
            board.king_not_moved[move.king_changes] = true;
        if (move.rook_changes != byte.MaxValue)
        {
            for (int i = 0; i < 4; i++)
            {
                if ((move.rook_changes & (byte)(1 << i)) != 0)
                    board.rook_not_moved[i] = true;
            }
        }

        //put pieces back were they where
        for (int i = 0; i < move.moved_piece_idx; i++)
        {
            int from = move.moved_pieces[i, 1];
            int to = move.moved_pieces[i, 0];

            board.piece_square_lists[board.board[from]][board.idx_board[from]] = (byte)to;

            //idx board
            board.idx_board[to] = board.idx_board[from];
            board.idx_board[from] = standart_chess.no_square;

            //color boards
            board.boards[board.color, to] = board.boards[board.color, from];
            board.boards[board.color, from] = standart_chess.no_piece;

            //real board
            board.board[to] = board.board[from];
            board.board[from] = standart_chess.no_piece;
        }

        //sort captures
        for (int i = move.removed_piece_idx - 1; i >= 0; i--)
        {
            board = exchange_pieces(board, (byte)move.removed_pieces[i, 0], (byte)move.removed_pieces[i, 1]);
        }

        return board;
    }
    public bool check_representation_continuity(position position)
    {
        //continuity on the index board
        for (int i = 0; i < position.idx_board.Length; i++)
        {
            if (position.board[i] != standart_chess.no_piece)
            {
                if (position.idx_board[i] >= position.piececount[position.board[i]])
                    return false;
                if (position.piece_square_lists[position.board[i]][position.idx_board[i]] != i)
                    return false;
            }
        }

        return true;
    }
    public position pawn_execute_move(position board, byte from, byte to, byte other, bool use_reverse_move, reverse_move undo_move)
    {
        if (other != standart_chess.no_promotion)
        {
            switch (other)
            {
                case standart_chess.double_pawn_move:
                    board.en_passent_square = (byte)((from + to) / 2);
                    break;
                case standart_chess.castle_or_en_passent:
                    board.en_passent_square = standart_chess.no_square;
                    byte square_of_victim = (byte)((chess_stuff.get_y_from_square(from) << 3) ^ chess_stuff.get_x_from_square(to));
                    board = remove_piece_from_list(board, (byte)(standart_chess.pawn ^ ((board.color ^ 1) << 3)), square_of_victim);
                    board.board[square_of_victim] = standart_chess.no_piece;
                    board.boards[board.color ^ 1, square_of_victim] = standart_chess.no_piece;
                    if (use_reverse_move)
                    {
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 0] = square_of_victim;
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 1] = standart_chess.pawn ^ ((board.color ^ 1) << 3);
                        undo_move.removed_piece_idx++;
                    }
                    break;
                case standart_chess.knight_promotion:
                    board = exchange_pieces(board, from, (byte)(standart_chess.knight ^ (board.color << 3)));
                    if (use_reverse_move)
                    {
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 0] = from;
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 1] = standart_chess.pawn ^ (board.color << 3);
                        undo_move.removed_piece_idx++;
                    }
                    break;
                case standart_chess.bishop_promotion:
                    board = exchange_pieces(board, from, (byte)(standart_chess.bishop ^ (board.color << 3)));
                    if (use_reverse_move)
                    {
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 0] = from;
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 1] = standart_chess.pawn ^ (board.color << 3);
                        undo_move.removed_piece_idx++;
                    }
                    break;
                case standart_chess.rook_promotion:
                    board = exchange_pieces(board, from, (byte)(standart_chess.rook ^ (board.color << 3)));
                    if (use_reverse_move)
                    {
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 0] = from;
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 1] = standart_chess.pawn ^ (board.color << 3);
                        undo_move.removed_piece_idx++;
                    }
                    break;
                case standart_chess.queen_promotion:
                    board = exchange_pieces(board, from, (byte)(standart_chess.queen ^ (board.color << 3)));
                    if (use_reverse_move)
                    {
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 0] = from;
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 1] = standart_chess.pawn ^ (board.color << 3);
                        undo_move.removed_piece_idx++;
                    }
                    break;
            }
        }
        return normal_piece_execute_move(board, from, to, use_reverse_move, undo_move);
    }
    public position exchange_pieces(position board, byte square, byte new_piece)
    {
        if (board.board[square] != standart_chess.no_piece) board = remove_piece_from_list(board, board.board[square], square);
        board = add_piece_to_list(board, new_piece, square);
        board.board[square] = new_piece;
        board.boards[chess_stuff.get_piece_color(new_piece), square] = (byte)(new_piece & 0b00000111);

        return board;
    }
    public position king_execute_move(position board, byte from, byte to, byte other, bool reverse_move_update, reverse_move undo_move)
    {
        if (board.king_not_moved[board.color])
        {
            board.king_not_moved[board.color] = false;
            if (reverse_move_update) undo_move.king_changes = board.color;
        }
        //castling
        if (other == standart_chess.castle_or_en_passent)
        {
            //look for illegalities
            if (!check(board, false))
            {
                board = normal_piece_execute_move(board, from, (byte)((from + to) / 2), false, null);
                if (!check(board, false))
                {
                    board = normal_piece_execute_move(board, (byte)((from + to) / 2), from, false, null);
                    //castling is legal
                    if (to > from)
                        return normal_piece_execute_move(normal_piece_execute_move(board, from, to, reverse_move_update, undo_move), (byte)(to + 1), (byte)(from + 1), reverse_move_update, undo_move);

                    return normal_piece_execute_move(normal_piece_execute_move(board, from, to, reverse_move_update, undo_move), (byte)(to - 2), (byte)(from - 1), reverse_move_update, undo_move);
                }
                else
                {
                    board = normal_piece_execute_move(board, (byte)((from + to) / 2), from, false, null);
                    illegal_position = true;
                    return board;
                }
            }
            //illegal move
            else
            {
                illegal_position = true;
                return board;
            }
        }

        return normal_piece_execute_move(board, from, to, reverse_move_update, undo_move);
    }
    public bool rook_consistency(position board)
    {
        byte othercolor = (byte)(board.color ^ 1);

        if (board.boards[othercolor, start_rook_square[othercolor, 0]] != standart_chess.rook && board.rook_not_moved[(othercolor << 1) ^ 0] && board.king_not_moved[othercolor])
            return false;

        if (board.boards[othercolor, start_rook_square[othercolor, 1]] != standart_chess.rook && board.rook_not_moved[(othercolor << 1) ^ 1] && board.king_not_moved[othercolor])
            return false;

        return true;
    }
    public position normal_piece_execute_move(position board, byte from, byte to, bool reverse_move_update, reverse_move undo_move)
    {
        if (reverse_move_update)
        {
            undo_move.moved_pieces[undo_move.moved_piece_idx, 0] = from;
            undo_move.moved_pieces[undo_move.moved_piece_idx, 1] = to;
            undo_move.moved_piece_idx++;
        }

        //update the friendly piece list
        board.piece_square_lists[board.board[from]][board.idx_board[from]] = (byte)to;

        //if capture update the enemy piece list
        if (board.board[to] != standart_chess.no_piece)
        {
            if (reverse_move_update)
            {
                undo_move.removed_pieces[undo_move.removed_piece_idx, 0] = to;
                undo_move.removed_pieces[undo_move.removed_piece_idx, 1] = board.board[to];
                undo_move.removed_piece_idx++;
            }
            reset_fifty_move_counter = true;
            board = remove_piece_from_list(board, board.board[to], to);
        }

        //idx board
        board.idx_board[to] = board.idx_board[from];
        board.idx_board[from] = standart_chess.no_square;

        //color boards
        board.boards[board.color, to] = board.boards[board.color, from];
        board.boards[board.color, from] = standart_chess.no_piece;
        board.boards[board.color ^ 1, to] = standart_chess.no_piece;

        //real board
        board.board[to] = board.board[from];
        board.board[from] = standart_chess.no_piece;

        return board;
    }
    public position remove_piece_from_list(position board, byte piecetype, byte square)
    {
        board.piececount[piecetype]--;
        board.piece_square_lists[piecetype][board.idx_board[square]] = board.piece_square_lists[piecetype][board.piececount[piecetype]];
        board.idx_board[board.piece_square_lists[piecetype][board.idx_board[square]]] = board.idx_board[square];
        board.idx_board[square] = standart_chess.no_square;
        return board;
    }
    public position add_piece_to_list(position board, byte piecetype, byte square)
    {
        board.piece_square_lists[piecetype][board.piececount[piecetype]] = square;
        board.idx_board[square] = board.piececount[piecetype];
        board.piececount[piecetype]++;

        return board;
    }
    public short move_x_to_y(byte from, byte to, byte extra)
    {
        return (short)(from ^ (to << 6) ^ (extra << 12));
    }
    public int[] add_pawn_moves_to_list(int[] movelist, byte from, byte to, bool promotion)
    {
        if (promotion)
        {
            for (byte i = standart_chess.knight_promotion; i < standart_chess.queen_promotion + 1; i++)
            {
                movelist[move_idx] = move_x_to_y(from, to, i);
                move_idx++;
            }
        }
        else
        {
            movelist[move_idx] = move_x_to_y(from, to, 0);
            move_idx++;
        }

        return movelist;
    }
    public int[] pawn_move(int[] movelist, position board, byte square)
    {
        int y_dir = (2 * board.color - 1) * iny;
        byte x = chess_stuff.get_x_from_square(square);
        byte y = chess_stuff.get_y_from_square(square);
        byte other_color = (byte)(1 - board.color);
        bool promotion = board.color == 1 && y == 6 || board.color == 0 && y == 1;
        byte new_square = (byte)(square + y_dir);

        //normal move up
        if (board.board[new_square] == standart_chess.no_piece)
        {
            movelist = add_pawn_moves_to_list(movelist, square, new_square, promotion);

            //double move
            if (y + 5 * board.color == 6 && board.board[new_square + y_dir] == standart_chess.no_piece)
            {
                movelist[move_idx] = move_x_to_y(square, (byte)(new_square + y_dir), standart_chess.double_pawn_move);
                move_idx++;
            }
        }

        //take an enemy
        if (x + 1 < 8 && board.boards[other_color, new_square + 1] != 0)
            movelist = add_pawn_moves_to_list(movelist, square, (byte)(new_square + 1), promotion);

        if (x - 1 >= 0 && board.boards[other_color, new_square - 1] != 0)
            movelist = add_pawn_moves_to_list(movelist, square, (byte)(new_square - 1), promotion);

        //en passent
        if (board.en_passent_square != standart_chess.no_square &&
            y + y_dir / iny == chess_stuff.get_y_from_square(board.en_passent_square) &&
            Math.Abs(chess_stuff.get_x_from_square(board.en_passent_square) - x) == 1)
        {
            movelist[move_idx] = move_x_to_y(square, board.en_passent_square, standart_chess.castle_or_en_passent);
            move_idx++;
        }

        return movelist;
    }
    public int[] pawn_capture(int[] movelist, position board, byte square)
    {
        int y_dir = (2 * board.color - 1) * iny;
        byte x = chess_stuff.get_x_from_square(square);
        byte y = chess_stuff.get_y_from_square(square);
        byte other_color = (byte)(1 - board.color);
        bool promotion = board.color == 1 && y == 6 || board.color == 0 && y == 1;
        byte new_square = (byte)(square + y_dir);

        //take an enemy
        if (x + 1 < 8 && board.boards[other_color, new_square + 1] != 0)
            movelist = add_pawn_moves_to_list(movelist, square, (byte)(new_square + 1), promotion);

        if (x - 1 >= 0 && board.boards[other_color, new_square - 1] != 0)
            movelist = add_pawn_moves_to_list(movelist, square, (byte)(new_square - 1), promotion);

        //en passent
        if (board.en_passent_square != standart_chess.no_square &&
            y + y_dir / iny == chess_stuff.get_y_from_square(board.en_passent_square) &&
            Math.Abs(chess_stuff.get_x_from_square(board.en_passent_square) - x) == 1)
        {
            movelist[move_idx] = move_x_to_y(square, board.en_passent_square, standart_chess.castle_or_en_passent);
            move_idx++;
        }

        return movelist;
    }
    public void init_knight_move()
    {
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                byte current_square = (byte)((y << 3) ^ x);
                List<byte> movelist = new List<byte>();

                int new_x = x + 2, new_y = 0;
                if (new_x < 8)
                {
                    new_y = y - 1;
                    if (new_y >= 0)
                        movelist.Add((byte)(new_x ^ (new_y << 3)));

                    new_y = y + 1;
                    if (new_y < 8)
                        movelist.Add((byte)(new_x ^ (new_y << 3)));
                }
                new_x -= 4;
                if (new_x >= 0)
                {
                    new_y = y + 1;
                    if (new_y < 8)
                        movelist.Add((byte)(new_x ^ (new_y << 3)));

                    new_y = y - 1;
                    if (new_y >= 0)
                        movelist.Add((byte)(new_x ^ (new_y << 3)));
                }
                new_x += 3;
                if (new_x < 8)
                {
                    new_y = y - 2;
                    if (new_y >= 0)
                        movelist.Add((byte)(new_x ^ (new_y << 3)));

                    new_y = y + 2;
                    if (new_y < 8)
                        movelist.Add((byte)(new_x ^ (new_y << 3)));
                }
                new_x -= 2;
                if (new_x >= 0)
                {
                    new_y = y + 2;
                    if (new_y < 8)
                        movelist.Add((byte)(new_x ^ (new_y << 3)));

                    new_y = y - 2;
                    if (new_y >= 0)
                        movelist.Add((byte)(new_x ^ (new_y << 3)));
                }

                knight_table[current_square] = new byte[movelist.Count];
                Array.Copy(movelist.ToArray(), knight_table[current_square], movelist.Count);
            }
        }
    }
    public int[] knight_move(int[] movelist, position board, byte square)
    {
        byte[] possible_squares = knight_table[square];
        for (int i = 0; i < possible_squares.Length; i++)
        {
            byte new_square = possible_squares[i];

            if (board.boards[board.color, new_square] == standart_chess.no_piece)
            {
                movelist[move_idx] = move_x_to_y(square, new_square, standart_chess.no_promotion);
                move_idx++;
            }
        }
        return movelist;
    }
    public int[] knight_capture(int[] movelist, position board, byte square)
    {
        byte[] possible_squares = knight_table[square];
        for (int i = 0; i < possible_squares.Length; i++)
        {
            byte new_square = possible_squares[i];

            if (board.boards[board.color ^ 1, new_square] != standart_chess.no_piece)
            {
                movelist[move_idx] = move_x_to_y(square, new_square, standart_chess.no_promotion);
                move_idx++;
            }
        }
        return movelist;
    }
    public int[] king_move(int[] movelist, position board, byte square)
    {
        //normal moves
        byte[] possible_squares = king_table[square];
        for (int i = 0; i < possible_squares.Length; i++)
        {
            byte new_square = possible_squares[i];
            if (board.boards[board.color, new_square] == standart_chess.no_piece)
            {
                movelist[move_idx] = move_x_to_y(square, new_square, standart_chess.no_promotion);
                move_idx++;
            }
        }

        //castling
        if (board.king_not_moved[board.color])
        {
            Debug.Assert(square == 60 || square == 4);
            //queenside castling
            if (board.rook_not_moved[(board.color << 1) ^ 0] && board.board[square - 1] == standart_chess.no_piece && board.board[square - 2] == standart_chess.no_piece && board.board[square - 3] == standart_chess.no_piece)
            {
                movelist[move_idx] = move_x_to_y(square, (byte)(square - 2), standart_chess.castle_or_en_passent);
                move_idx++;
            }
            //kingside castling
            if (board.rook_not_moved[(board.color << 1) ^ 1] && board.board[square + 1] == standart_chess.no_piece && board.board[square + 2] == standart_chess.no_piece)
            {
                movelist[move_idx] = move_x_to_y(square, (byte)(square + 2), standart_chess.castle_or_en_passent);
                move_idx++;
            }
        }

        return movelist;
    }
    public int[] king_capture(int[] movelist, position board, byte square)
    {
        //normal moves
        byte[] possible_squares = king_table[square];
        for (int i = 0; i < possible_squares.Length; i++)
        {
            byte new_square = possible_squares[i];
            if (board.boards[board.color ^ 1, new_square] != standart_chess.no_piece)
            {
                movelist[move_idx] = move_x_to_y(square, new_square, standart_chess.no_promotion);
                move_idx++;
            }
        }

        return movelist;
    }
    public int[] sliding_piece_move(int[] movelist, position board, byte square, byte[,][] array)
    {
        for (int i = 0; i < 4; i++)
        {
            byte[] possible_squares = array[square, i];

            for (int j = 0; j < possible_squares.Length; j++)
            {
                byte new_square = possible_squares[j];

                if (board.board[new_square] == standart_chess.no_piece)
                {
                    movelist[move_idx] = move_x_to_y(square, new_square, standart_chess.no_promotion);
                    move_idx++;
                }
                else
                {
                    if (board.boards[board.color, new_square] == standart_chess.no_piece)
                    {
                        movelist[move_idx] = move_x_to_y(square, new_square, standart_chess.no_promotion);
                        move_idx++;
                    }

                    break;
                }
            }
        }

        return movelist;
    }
    public int[] sliding_piece_capture(int[] movelist, position board, byte square, byte[,][] array)
    {
        for (int i = 0; i < 4; i++)
        {
            byte[] possible_squares = array[square, i];

            for (int j = 0; j < possible_squares.Length; j++)
            {
                byte new_square = possible_squares[j];

                if(board.board[new_square] != standart_chess.no_piece)
                { 
                if (board.boards[board.color ^ 1, new_square] != standart_chess.no_piece)
                {
                    movelist[move_idx] = move_x_to_y(square, new_square, standart_chess.no_promotion);
                    move_idx++;
                }
                    break;
                }
            }
        }

        return movelist;
    }
    public void check_vector_for_check(position board, byte square, byte[,][] array, int direction, byte piece_color, byte piece)
    {
        byte[] possible_squares = array[square, direction];

        for (int j = 0; j < possible_squares.Length; j++)
        {
            byte new_square = possible_squares[j];

            if (board.board[new_square] != standart_chess.no_piece)
            {
                illegal_position = board.boards[piece_color, new_square] == piece || illegal_position;

                return;
            }
        }
    }
    public void init_bishop_move()
    {
        List<byte> movelist = new List<byte>();
        int new_x, new_y;
        for (int current_x = 0; current_x < 8; current_x++)
        {
            for (int current_y = 0; current_y < 8; current_y++)
            {
                byte current_square = (byte)((current_y << 3) ^ current_x);
                movelist = new List<byte>();
                for (int i = 1; i + current_x < 8 && i + current_y < 8; i++)
                {
                    new_x = current_x + i;
                    new_y = current_y + i;

                    movelist.Add((byte)(new_x ^ (new_y << 3)));
                }
                bishop_table[current_square, 0] = new byte[movelist.Count];
                Array.Copy(movelist.ToArray(), bishop_table[current_square, 0], movelist.Count);

                movelist = new List<byte>();
                for (int i = 1; current_x - i >= 0 && current_y - i >= 0; i++)
                {
                    new_x = current_x - i;
                    new_y = current_y - i;

                    movelist.Add((byte)(new_x ^ (new_y << 3)));
                }
                bishop_table[current_square, 1] = new byte[movelist.Count];
                Array.Copy(movelist.ToArray(), bishop_table[current_square, 1], movelist.Count);

                movelist = new List<byte>();
                for (int i = 1; current_x - i >= 0 && i + current_y < 8; i++)
                {
                    new_x = current_x - i;
                    new_y = current_y + i;

                    movelist.Add((byte)(new_x ^ (new_y << 3)));
                }
                bishop_table[current_square, 2] = new byte[movelist.Count];
                Array.Copy(movelist.ToArray(), bishop_table[current_square, 2], movelist.Count);

                movelist = new List<byte>();
                for (int i = 1; i + current_x < 8 && current_y - i >= 0; i++)
                {
                    new_x = current_x + i;
                    new_y = current_y - i;

                    movelist.Add((byte)(new_x ^ (new_y << 3)));
                }
                bishop_table[current_square, 3] = new byte[movelist.Count];
                Array.Copy(movelist.ToArray(), bishop_table[current_square, 3], movelist.Count);
            }
        }
    }
    public void init_rook_move()
    {
        List<byte> movelist = new List<byte>();
        for (int current_x = 0; current_x < 8; current_x++)
        {
            for (int current_y = 0; current_y < 8; current_y++)
            {
                byte current_square = (byte)((current_y << 3) ^ current_x);

                movelist = new List<byte>();
                for (int i = current_x + 1; i < 8; i++)
                    movelist.Add((byte)(i ^ (current_y << 3)));
                rook_table[current_square, 0] = new byte[movelist.Count];
                Array.Copy(movelist.ToArray(), rook_table[current_square, 0], movelist.Count);

                movelist = new List<byte>();
                for (int i = current_x - 1; i >= 0; i--)
                    movelist.Add((byte)(i ^ (current_y << 3)));
                rook_table[current_square, 1] = new byte[movelist.Count];
                Array.Copy(movelist.ToArray(), rook_table[current_square, 1], movelist.Count);

                movelist = new List<byte>();
                for (int i = current_y + 1; i < 8; i++)
                    movelist.Add((byte)(current_x ^ (i << 3)));
                rook_table[current_square, 2] = new byte[movelist.Count];
                Array.Copy(movelist.ToArray(), rook_table[current_square, 2], movelist.Count);

                movelist = new List<byte>();
                for (int i = current_y - 1; i >= 0; i--)
                    movelist.Add((byte)(current_x ^ (i << 3)));
                rook_table[current_square, 3] = new byte[movelist.Count];
                Array.Copy(movelist.ToArray(), rook_table[current_square, 3], movelist.Count);
            }
        }
    }

    public void init_king_move()
    {
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                List<byte> movelist = new List<byte>();
                byte current_square = (byte)((y << 3) ^ x);

                if (y + 1 < 8)
                {
                    if (x + 1 < 8)
                        movelist.Add((byte)((x + 1) ^ ((y + 1) << 3)));
                    if (x - 1 >= 0)
                        movelist.Add((byte)((x - 1) ^ ((y + 1) << 3)));

                    movelist.Add((byte)(x ^ ((y + 1) << 3)));
                }

                if (y - 1 >= 0)
                {
                    if (x + 1 < 8)
                        movelist.Add((byte)((x + 1) ^ ((y - 1) << 3)));
                    if (x - 1 >= 0)
                        movelist.Add((byte)((x - 1) ^ ((y - 1) << 3)));

                    movelist.Add((byte)(x ^ ((y - 1) << 3)));
                }

                if (x + 1 < 8)
                    movelist.Add((byte)((x + 1) ^ (y << 3)));
                if (x - 1 >= 0)
                    movelist.Add((byte)((x - 1) ^ (y << 3)));

                king_table[current_square] = new byte[movelist.Count];
                Array.Copy(movelist.ToArray(), king_table[current_square], movelist.Count);
            }
        }
    }
}
class reverse_move
{
    public byte en_passent = byte.MaxValue, fifty_move_rule = 0, king_changes = byte.MaxValue, rook_changes = byte.MaxValue, moved_piece_idx = 0, removed_piece_idx = 0;
    public int[,] moved_pieces = new int[2, 2], removed_pieces = new int[2, 2];
}
