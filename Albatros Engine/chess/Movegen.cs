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
    StandartChess chess_stuff = new StandartChess();
    int[,] start_rook_square = new int[2, 2] { { 56, 63 }, { 0, 7 } };
    public bool illegal_position = false;
    public bool nonPawnMaterial = false;
    bool reset_fifty_move_counter = false;
    public int moveIdx = 0;
    public movegen()
    {
        init_knight_move();
        init_bishop_move();
        init_rook_move();
        init_king_move();
    }
    public int is_mate(Position board, bool in_check, ReverseMove undo_move, int[] movelist)
    {
        //generate pseudolegal_movelist
        movelist = generate_movelist(board, movelist);

        //loop through each move
        for (int i = 0; i < moveIdx; i++)
        {
            if (illegality_check_neccessaray(movelist[i], in_check, board))
            {
                //make move on the board
                board = make_move(board, movelist[i], true, undo_move);

                //look if the position is illegal
                if (check(board, true))
                {
                    moveIdx--;
                    movelist[i] = movelist[moveIdx];
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
    public int[] generate_movelist(Position board, int[] movelist)
    {
        moveIdx = 0;
        nonPawnMaterial = false;
        //loop through each piece with the current color
        for (byte i = 0; i < 64; i++)
        {
            if (board.boards[board.color, i] != 0)
            {
                byte piece_wo_color = board.boards[board.color, i];

                switch (piece_wo_color)
                {
                    case StandartChess.pawn:
                        movelist = pawn_move(movelist, board, i);
                        break;
                    case StandartChess.knight:
                        nonPawnMaterial = true;
                        movelist = knight_move(movelist, board, i);
                        break;
                    case StandartChess.bishop:
                        nonPawnMaterial = true;
                        movelist = sliding_piece_move(movelist, board, i, bishop_table);
                        break;
                    case StandartChess.rook:
                        nonPawnMaterial = true;
                        movelist = sliding_piece_move(movelist, board, i, rook_table);
                        break;
                    case StandartChess.queen:
                        nonPawnMaterial = true;
                        movelist = sliding_piece_move(sliding_piece_move(movelist, board, i, bishop_table), board, i, rook_table);
                        break;
                    case StandartChess.king:
                        nonPawnMaterial = true;
                        movelist = king_move(movelist, board, i);
                        break;
                }
            }
        }
        return movelist;
    }
    public int[] generate_capture_list(Position board, int[] movelist)
    {
        moveIdx = 0;
        //loop through each piece with the current color
        for (byte i = 0; i < 64; i++)
        {
            if (board.boards[board.color, i] != 0)
            {
                byte piece_wo_color = board.boards[board.color, i];

                switch (piece_wo_color)
                {
                    case StandartChess.pawn:
                        movelist = pawn_capture(movelist, board, i);
                        break;
                    case StandartChess.knight:
                        movelist = knight_capture(movelist, board, i);
                        break;
                    case StandartChess.bishop:
                        movelist = sliding_piece_capture(movelist, board, i, bishop_table);
                        break;
                    case StandartChess.rook:
                        movelist = sliding_piece_capture(movelist, board, i, rook_table);
                        break;
                    case StandartChess.queen:
                        movelist = sliding_piece_capture(sliding_piece_capture(movelist, board, i, bishop_table), board, i, rook_table);
                        break;
                    case StandartChess.king:
                        movelist = king_capture(movelist, board, i);
                        break;
                }
            }
        }
        return movelist;
    }
    public int[] LegalMoveGenerator(Position board, bool in_check, ReverseMove undo_move, int[] movelist)
    {
        //generate pseudolegal_movelist
        int[] first_movelist = generate_movelist(board, movelist);
        
        //loop through each move
        for (int i = 0; i < moveIdx; i++)
        {
            if (illegality_check_neccessaray(first_movelist[i], in_check, board))
            {
                //make move on the board
                board = make_move(board, first_movelist[i], true, undo_move);

                //look if the position is illegal
                if (check(board, true))
                {
                    moveIdx--;
                    first_movelist[i] = first_movelist[moveIdx];
                    i--;
                }

                //make the reverse move
                board = unmake_move(board, undo_move);
            }
        }
        return first_movelist;
    }
    public int[] LegalCaptureGenerator(Position board, bool in_check, ReverseMove undo_move, int[] movelist)
    {
        //generate pseudolegal_movelist
        int[] first_movelist = generate_capture_list(board, movelist);

        //loop through each move
        for (int i = 0; i < moveIdx; i++)
        {
            if (illegality_check_neccessaray(first_movelist[i], in_check, board))
            {
                //make move on the board
                board = make_move(board, first_movelist[i], true, undo_move);

                //look if the position is illegal
                if (check(board, true))
                {
                    moveIdx--;
                    first_movelist[i] = first_movelist[moveIdx];
                    i--;
                }

                //make the reverse move
                board = unmake_move(board, undo_move);
            }
        }

        return first_movelist;
    }
    public bool fast_check(Position board, int move)
    {
        byte other = (byte)(move >> 12);
        byte from = (byte)(move & 0b0000000000111111);
        byte to = (byte)((move & 0b0000111111000000) >> 6);

        int king_square = board.piece_square_lists[StandartChess.king ^ (board.color << 3)][0];
        int othercolor = board.color ^ 1;
        int displaced_other_color = othercolor << 3;
        int king_x = chess_stuff.get_x_from_square((byte)king_square);
        int king_y = chess_stuff.GetY((byte)king_square);

        //only special cases
        if (other == StandartChess.castle_or_en_passent)
        {
            if(board.boards[othercolor, to] == StandartChess.king )
            { 
                //find the square on wich the rook landed
                byte rook_square = (byte)(from + ((from < to) ? 1 : -1));

                int rook_x = chess_stuff.get_x_from_square((byte)rook_square);
                //calculate the first delta
                if (king_x == rook_x)
                    check_vector_for_check(board, (byte)king_square, rook_table, king_square - rook_square < 0 ? 2 : 3, (byte)othercolor, StandartChess.rook);
                else if (king_square - rook_square == king_x - rook_x)
                    check_vector_for_check(board, (byte)king_square, rook_table, king_x - rook_x < 0 ? 0 : 1, (byte)othercolor, StandartChess.rook);

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
            case StandartChess.queen:
                //calculate the first delta
                if (king_x == to_x)
                    check_vector_for_check(board, (byte)king_square, rook_table, king_square - to < 0 ? 2 : 3, (byte)othercolor, StandartChess.queen);
                else
                {
                    int delta_a = king_square - to;
                    int delta_b = king_x - to_x;

                    if (delta_a == delta_b)
                        check_vector_for_check(board, (byte)king_square, rook_table, king_x - to_x < 0 ? 0 : 1, (byte)othercolor, StandartChess.queen);
                    else if (delta_a == 9 * delta_b)
                        check_vector_for_check(board, (byte)king_square, bishop_table, king_x - to_x < 0 ? 0 : 1, (byte)othercolor, StandartChess.queen);
                    else if (delta_a == -7 * delta_b)
                        check_vector_for_check(board, (byte)king_square, bishop_table, king_x - to_x < 0 ? 3 : 2, (byte)othercolor, StandartChess.queen);
                }
                break;
            case StandartChess.rook:
                //calculate the first delta
                if (king_x == to_x)
                    check_vector_for_check(board, (byte)king_square, rook_table, king_square - to < 0 ? 2 : 3, (byte)othercolor, StandartChess.rook);
                else if (king_square - to == king_x - to_x)
                    check_vector_for_check(board, (byte)king_square, rook_table, king_x - to_x < 0 ? 0 : 1, (byte)othercolor, StandartChess.rook);
                break;
            case StandartChess.bishop:
                //calculate the first delta
                if (king_x != to_x)
                {
                    int delta_a = king_square - to;
                    int delta_b = king_x - to_x;

                    if (delta_a == 9 * delta_b)
                        check_vector_for_check(board, (byte)king_square, bishop_table, king_x - to_x < 0 ? 0 : 1, (byte)othercolor, StandartChess.bishop);
                    if (delta_a == -7 * delta_b)
                        check_vector_for_check(board, (byte)king_square, bishop_table, king_x - to_x < 0 ? 3 : 2, (byte)othercolor, StandartChess.bishop);
                }
                break;
            case StandartChess.knight:
                int delta_x = Math.Abs((int)king_x - to_x);
                if (delta_x == 1 || delta_x == 2)
                {
                    int to_y = chess_stuff.GetY(to);
                    int delta_y = Math.Abs((int)king_y - to_y);
                    if (delta_y == 3 - delta_x)
                        return true;
                }
                break;
            case StandartChess.pawn:
                int y_dir = (1 - 2 * othercolor) * iny;
                if (king_square + y_dir < 64 && king_square + y_dir >= 0)
                {
                    if (king_x - 1 >= 0 && board.boards[othercolor, king_square - 1 + y_dir] == StandartChess.pawn)
                        return true;

                    if (king_x + 1 < 8 && board.boards[othercolor, king_square + 1 + y_dir] == StandartChess.pawn)
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
    public void from_check(Position board,byte king_square, byte king_x, byte from, byte from_x, byte othercolor)
    {
        if (king_x == from_x)
        {
            check_vector_for_check(board, (byte)king_square, rook_table, king_square - from < 0 ? 2 : 3, (byte)othercolor, StandartChess.queen);
            check_vector_for_check(board, (byte)king_square, rook_table, king_square - from < 0 ? 2 : 3, (byte)othercolor, StandartChess.rook);
        }
        else
        {
            int delta_a = king_square - from;
            int delta_b = king_x - from_x;

            if (delta_a == delta_b)
            {
                check_vector_for_check(board, (byte)king_square, rook_table, king_x - from_x < 0 ? 0 : 1, (byte)othercolor, StandartChess.rook);
                check_vector_for_check(board, (byte)king_square, rook_table, king_x - from_x < 0 ? 0 : 1, (byte)othercolor, StandartChess.queen);
            }
            else if (delta_a == 9 * delta_b)
            {
                check_vector_for_check(board, (byte)king_square, bishop_table, king_x - from_x < 0 ? 0 : 1, (byte)othercolor, StandartChess.bishop);
                check_vector_for_check(board, (byte)king_square, bishop_table, king_x - from_x < 0 ? 0 : 1, (byte)othercolor, StandartChess.queen);
            }
            else if (delta_a == -7 * delta_b)
            {
                check_vector_for_check(board, (byte)king_square, bishop_table, king_x - from_x < 0 ? 3 : 2, (byte)othercolor, StandartChess.bishop);
                check_vector_for_check(board, (byte)king_square, bishop_table, king_x - from_x < 0 ? 3 : 2, (byte)othercolor, StandartChess.queen);
            }
        }
    }
    public bool illegality_check_neccessaray(int move, bool in_check, Position board)
    {
        int from = move & 0b0000000000111111;
        if (in_check || board.boards[board.color, from] == StandartChess.king)
            return true;

        int from_x = chess_stuff.get_x_from_square((byte)from);
        int king_square = board.piece_square_lists[StandartChess.king ^ (board.color << 3)][0];
        int king_x = chess_stuff.get_x_from_square((byte)king_square);

        if (from_x == king_x)
            return true;

        int delta_a = king_square - from;
        int delta_b = king_x - from_x;

        if (delta_a == delta_b || delta_a == 9 * delta_b || delta_a == -7 * delta_b)
            return true;


        return false;
    }
    public bool check(Position board, bool illegality_check)
    {
        //find king 
        byte othercolor = illegality_check ? board.color : (byte)(board.color ^ 1);
        int displaced_other_color = othercolor << 3;
        int king_square = board.piece_square_lists[StandartChess.king ^ ((othercolor ^ 1) << 3)][0];
        int king_x = chess_stuff.get_x_from_square((byte)king_square);
        int king_y = chess_stuff.GetY((byte)king_square);

        //the goal is to ckeck if the king is on the same diagonal as the piece
        //if this is the case just check the diagonal

        //queen
        for (int i = 0; i < board.piececount[StandartChess.queen ^ displaced_other_color]; i++)
        {
            byte queen_square = board.piece_square_lists[StandartChess.queen ^ displaced_other_color][i];
            int queen_x = chess_stuff.get_x_from_square((byte)queen_square);
            //calculate the first delta
            if (king_x == queen_x)
                check_vector_for_check(board, (byte)king_square, rook_table, king_square - queen_square < 0 ? 2 : 3, othercolor, StandartChess.queen);
            else
            {
                int delta_a = king_square - queen_square;
                int delta_b = king_x - queen_x;

                if (delta_a == delta_b)
                    check_vector_for_check(board, (byte)king_square, rook_table, king_x - queen_x < 0 ? 0 : 1, othercolor, StandartChess.queen);
                else if (delta_a == 9 * delta_b)
                    check_vector_for_check(board, (byte)king_square, bishop_table, king_x - queen_x < 0 ? 0 : 1, othercolor, StandartChess.queen);
                else if (delta_a == -7 * delta_b)
                    check_vector_for_check(board, (byte)king_square, bishop_table, king_x - queen_x < 0 ? 3 : 2, othercolor, StandartChess.queen);
            }
        }

        //rook
        for (int i = 0; i < board.piececount[StandartChess.rook ^ displaced_other_color]; i++)
        {
            byte rook_square = board.piece_square_lists[StandartChess.rook ^ displaced_other_color][i];
            int rook_x = chess_stuff.get_x_from_square((byte)rook_square);
            //calculate the first delta
            if (king_x == rook_x)
                check_vector_for_check(board, (byte)king_square, rook_table, king_square - rook_square < 0 ? 2 : 3, othercolor, StandartChess.rook);
            else if (king_square - rook_square == king_x - rook_x)
                check_vector_for_check(board, (byte)king_square, rook_table, king_x - rook_x < 0 ? 0 : 1, othercolor, StandartChess.rook);
        }

        //bishop
        for (int i = 0; i < board.piececount[StandartChess.bishop ^ displaced_other_color]; i++)
        {
            byte bishop_square = board.piece_square_lists[StandartChess.bishop ^ displaced_other_color][i];
            int bishop_x = chess_stuff.get_x_from_square((byte)bishop_square);
            //calculate the first delta
            if (king_x != bishop_x)
            {
                int delta_a = king_square - bishop_square;
                int delta_b = king_x - bishop_x;

                if (delta_a == 9 * delta_b)
                    check_vector_for_check(board, (byte)king_square, bishop_table, king_x - bishop_x < 0 ? 0 : 1, othercolor, StandartChess.bishop);
                if (delta_a == -7 * delta_b)
                    check_vector_for_check(board, (byte)king_square, bishop_table, king_x - bishop_x < 0 ? 3 : 2, othercolor, StandartChess.bishop);
            }
        }

        if (illegal_position)
        {
            illegal_position = false;
            return true;
        }

        //knight
        for (int i = 0; i < board.piececount[StandartChess.knight ^ displaced_other_color]; i++)
        {
            byte knight_square = board.piece_square_lists[StandartChess.knight ^ displaced_other_color][i];
            int knight_x = chess_stuff.get_x_from_square(knight_square);
            int delta_x = Math.Abs((int)king_x - knight_x);
            if (delta_x == 1 || delta_x == 2)
            {
                int knight_y = chess_stuff.GetY(knight_square);
                int delta_y = Math.Abs((int)king_y - knight_y);
                if (delta_y == 3 - delta_x)
                    return true;
            }
        }

        //pawns
        int y_dir = (1 - 2 * othercolor) * iny;
        if (king_square + y_dir < 64 && king_square + y_dir >= 0)
        {
            if (king_x - 1 >= 0 && board.boards[othercolor, king_square - 1 + y_dir] == StandartChess.pawn)
                return true;

            if (king_x + 1 < 8 && board.boards[othercolor, king_square + 1 + y_dir] == StandartChess.pawn)
                return true;
        }

        //king
        if (illegality_check)
        {
            int other_king_square = board.piece_square_lists[StandartChess.king ^ displaced_other_color][0];
            int other_k_x = chess_stuff.get_x_from_square((byte)other_king_square);
            if (other_k_x == king_x + 1 || other_k_x == king_x || other_k_x == king_x - 1)
            {
                int other_k_y = chess_stuff.GetY((byte)other_king_square);
                if (other_k_y == king_y + 1 || other_k_y == king_y || other_k_y == king_y - 1)
                    return true;
            }
        }
        return false;
    }
    public Position make_move(Position board, int move, bool generate_reverse_move, ReverseMove undo_move)
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
            undo_move.fiftyMoveRule = (byte)board.fiftyMoveRule;
            undo_move.enPassent = board.enPassentSquare;
        }

        //look for en passent
        if (board.enPassentSquare != StandartChess.no_square && other != StandartChess.doublePawnMove)
            ep_legal = true;

        //reset rook has moved for castling
        if (piece == StandartChess.rook || board.boards[board.color ^ 1, to] == StandartChess.rook)
        {
            byte rook_color, rook_square;
            if (piece == StandartChess.rook)
            {
                rook_color = board.color;
                rook_square = from;
                board = set_rook_moved(board, rook_square, rook_color, generate_reverse_move, undo_move);
            }

            if (board.boards[board.color ^ 1, to] == StandartChess.rook)
            {
                rook_color = (byte)(board.color ^ 1);
                rook_square = to;
                board = set_rook_moved(board, rook_square, rook_color, generate_reverse_move, undo_move);
            }
        }

        switch (piece)
        {
            case StandartChess.pawn:
                board.fiftyMoveRule = 0;
                board = pawn_execute_move(board, from, to, other, generate_reverse_move, undo_move);
                break;
            case StandartChess.king:
                board.fiftyMoveRule++;
                board = king_execute_move(board, from, to, other, generate_reverse_move, undo_move);
                break;
            default:
                board.fiftyMoveRule++;
                board = normal_piece_execute_move(board, from, to, generate_reverse_move, undo_move);
                break;
        }

        if (board.enPassentSquare != StandartChess.no_square && ep_legal)
            board.enPassentSquare = StandartChess.no_square;

        if (reset_fifty_move_counter)
        {
            board.fiftyMoveRule = 0;
            reset_fifty_move_counter = false;
        }

        board.color = (byte)(board.color ^ 1);

        return board;
    }
    public Position set_rook_moved(Position board, byte rook_square, byte rook_color, bool reverse_move_update, ReverseMove undo_move)
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
    public Position unmake_move(Position board, ReverseMove move)
    {
        board.color = (byte)(board.color ^ 1);
        board.enPassentSquare = move.enPassent;
        board.fiftyMoveRule = move.fiftyMoveRule;
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
            board.idx_board[from] = StandartChess.no_square;

            //color boards
            board.boards[board.color, to] = board.boards[board.color, from];
            board.boards[board.color, from] = StandartChess.no_piece;

            //real board
            board.board[to] = board.board[from];
            board.board[from] = StandartChess.no_piece;
        }

        //sort captures
        for (int i = move.removed_piece_idx - 1; i >= 0; i--)
        {
            board = exchange_pieces(board, (byte)move.removed_pieces[i, 0], (byte)move.removed_pieces[i, 1]);
        }

        return board;
    }
    public bool check_representation_continuity(Position position)
    {
        //continuity on the index board
        for (int i = 0; i < position.idx_board.Length; i++)
        {
            if (position.board[i] != StandartChess.no_piece)
            {
                if (position.idx_board[i] >= position.piececount[position.board[i]])
                    return false;
                if (position.piece_square_lists[position.board[i]][position.idx_board[i]] != i)
                    return false;
            }
        }

        return true;
    }
    public Position pawn_execute_move(Position board, byte from, byte to, byte other, bool use_reverse_move, ReverseMove undo_move)
    {
        if (other != StandartChess.no_promotion)
        {
            switch (other)
            {
                case StandartChess.doublePawnMove:
                    board.enPassentSquare = (byte)((from + to) / 2);
                    break;
                case StandartChess.castle_or_en_passent:
                    board.enPassentSquare = StandartChess.no_square;
                    byte square_of_victim = (byte)((chess_stuff.GetY(from) << 3) ^ chess_stuff.get_x_from_square(to));
                    board = remove_piece_from_list(board, (byte)(StandartChess.pawn ^ ((board.color ^ 1) << 3)), square_of_victim);
                    board.board[square_of_victim] = StandartChess.no_piece;
                    board.boards[board.color ^ 1, square_of_victim] = StandartChess.no_piece;
                    if (use_reverse_move)
                    {
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 0] = square_of_victim;
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 1] = StandartChess.pawn ^ ((board.color ^ 1) << 3);
                        undo_move.removed_piece_idx++;
                    }
                    break;
                case StandartChess.knight_promotion:
                    board = exchange_pieces(board, from, (byte)(StandartChess.knight ^ (board.color << 3)));
                    if (use_reverse_move)
                    {
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 0] = from;
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 1] = StandartChess.pawn ^ (board.color << 3);
                        undo_move.removed_piece_idx++;
                    }
                    break;
                case StandartChess.bishop_promotion:
                    board = exchange_pieces(board, from, (byte)(StandartChess.bishop ^ (board.color << 3)));
                    if (use_reverse_move)
                    {
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 0] = from;
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 1] = StandartChess.pawn ^ (board.color << 3);
                        undo_move.removed_piece_idx++;
                    }
                    break;
                case StandartChess.rook_promotion:
                    board = exchange_pieces(board, from, (byte)(StandartChess.rook ^ (board.color << 3)));
                    if (use_reverse_move)
                    {
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 0] = from;
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 1] = StandartChess.pawn ^ (board.color << 3);
                        undo_move.removed_piece_idx++;
                    }
                    break;
                case StandartChess.queen_promotion:
                    board = exchange_pieces(board, from, (byte)(StandartChess.queen ^ (board.color << 3)));
                    if (use_reverse_move)
                    {
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 0] = from;
                        undo_move.removed_pieces[undo_move.removed_piece_idx, 1] = StandartChess.pawn ^ (board.color << 3);
                        undo_move.removed_piece_idx++;
                    }
                    break;
            }
        }
        return normal_piece_execute_move(board, from, to, use_reverse_move, undo_move);
    }
    public Position exchange_pieces(Position board, byte square, byte new_piece)
    {
        if (board.board[square] != StandartChess.no_piece) board = remove_piece_from_list(board, board.board[square], square);
        board = add_piece_to_list(board, new_piece, square);
        board.board[square] = new_piece;
        board.boards[chess_stuff.get_piece_color(new_piece), square] = (byte)(new_piece & 0b00000111);

        return board;
    }
    public Position king_execute_move(Position board, byte from, byte to, byte other, bool reverse_move_update, ReverseMove undo_move)
    {
        if (board.king_not_moved[board.color])
        {
            board.king_not_moved[board.color] = false;
            if (reverse_move_update) undo_move.king_changes = board.color;
        }
        //castling
        if (other == StandartChess.castle_or_en_passent)
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
    public bool rook_consistency(Position board)
    {
        byte othercolor = (byte)(board.color ^ 1);

        if (board.boards[othercolor, start_rook_square[othercolor, 0]] != StandartChess.rook && board.rook_not_moved[(othercolor << 1) ^ 0] && board.king_not_moved[othercolor])
            return false;

        if (board.boards[othercolor, start_rook_square[othercolor, 1]] != StandartChess.rook && board.rook_not_moved[(othercolor << 1) ^ 1] && board.king_not_moved[othercolor])
            return false;

        return true;
    }
    public Position normal_piece_execute_move(Position board, byte from, byte to, bool reverse_move_update, ReverseMove undo_move)
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
        if (board.board[to] != StandartChess.no_piece)
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
        board.idx_board[from] = StandartChess.no_square;

        //color boards
        board.boards[board.color, to] = board.boards[board.color, from];
        board.boards[board.color, from] = StandartChess.no_piece;
        board.boards[board.color ^ 1, to] = StandartChess.no_piece;

        //real board
        board.board[to] = board.board[from];
        board.board[from] = StandartChess.no_piece;

        return board;
    }
    public Position remove_piece_from_list(Position board, byte piecetype, byte square)
    {
        board.piececount[piecetype]--;
        board.piece_square_lists[piecetype][board.idx_board[square]] = board.piece_square_lists[piecetype][board.piececount[piecetype]];
        board.idx_board[board.piece_square_lists[piecetype][board.idx_board[square]]] = board.idx_board[square];
        board.idx_board[square] = StandartChess.no_square;
        return board;
    }
    public Position add_piece_to_list(Position board, byte piecetype, byte square)
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
            for (byte i = StandartChess.knight_promotion; i < StandartChess.queen_promotion + 1; i++)
            {
                movelist[moveIdx] = move_x_to_y(from, to, i);
                moveIdx++;
            }
        }
        else
        {
            movelist[moveIdx] = move_x_to_y(from, to, 0);
            moveIdx++;
        }

        return movelist;
    }
    public int[] pawn_move(int[] movelist, Position board, byte square)
    {
        int y_dir = (2 * board.color - 1) * iny;
        byte x = chess_stuff.get_x_from_square(square);
        byte y = chess_stuff.GetY(square);
        byte other_color = (byte)(1 - board.color);
        bool promotion = board.color == 1 && y == 6 || board.color == 0 && y == 1;
        byte new_square = (byte)(square + y_dir);

        //normal move up
        if (board.board[new_square] == StandartChess.no_piece)
        {
            movelist = add_pawn_moves_to_list(movelist, square, new_square, promotion);

            //double move
            if (y + 5 * board.color == 6 && board.board[new_square + y_dir] == StandartChess.no_piece)
            {
                movelist[moveIdx] = move_x_to_y(square, (byte)(new_square + y_dir), StandartChess.doublePawnMove);
                moveIdx++;
            }
        }

        //take an enemy
        if (x + 1 < 8 && board.boards[other_color, new_square + 1] != 0)
            movelist = add_pawn_moves_to_list(movelist, square, (byte)(new_square + 1), promotion);

        if (x - 1 >= 0 && board.boards[other_color, new_square - 1] != 0)
            movelist = add_pawn_moves_to_list(movelist, square, (byte)(new_square - 1), promotion);

        //en passent
        if (board.enPassentSquare != StandartChess.no_square &&
            y + y_dir / iny == chess_stuff.GetY(board.enPassentSquare) &&
            Math.Abs(chess_stuff.get_x_from_square(board.enPassentSquare) - x) == 1)
        {
            movelist[moveIdx] = move_x_to_y(square, board.enPassentSquare, StandartChess.castle_or_en_passent);
            moveIdx++;
        }

        return movelist;
    }
    public int[] pawn_capture(int[] movelist, Position board, byte square)
    {
        int y_dir = (2 * board.color - 1) * iny;
        byte x = chess_stuff.get_x_from_square(square);
        byte y = chess_stuff.GetY(square);
        byte other_color = (byte)(1 - board.color);
        bool promotion = board.color == 1 && y == 6 || board.color == 0 && y == 1;
        byte new_square = (byte)(square + y_dir);

        //take an enemy
        if (x + 1 < 8 && board.boards[other_color, new_square + 1] != 0)
            movelist = add_pawn_moves_to_list(movelist, square, (byte)(new_square + 1), promotion);

        if (x - 1 >= 0 && board.boards[other_color, new_square - 1] != 0)
            movelist = add_pawn_moves_to_list(movelist, square, (byte)(new_square - 1), promotion);

        //en passent
        if (board.enPassentSquare != StandartChess.no_square &&
            y + y_dir / iny == chess_stuff.GetY(board.enPassentSquare) &&
            Math.Abs(chess_stuff.get_x_from_square(board.enPassentSquare) - x) == 1)
        {
            movelist[moveIdx] = move_x_to_y(square, board.enPassentSquare, StandartChess.castle_or_en_passent);
            moveIdx++;
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
    public int[] knight_move(int[] movelist, Position board, byte square)
    {
        byte[] possible_squares = knight_table[square];
        for (int i = 0; i < possible_squares.Length; i++)
        {
            byte new_square = possible_squares[i];

            if (board.boards[board.color, new_square] == StandartChess.no_piece)
            {
                movelist[moveIdx] = move_x_to_y(square, new_square, StandartChess.no_promotion);
                moveIdx++;
            }
        }
        return movelist;
    }
    public int[] knight_capture(int[] movelist, Position board, byte square)
    {
        byte[] possible_squares = knight_table[square];
        for (int i = 0; i < possible_squares.Length; i++)
        {
            byte new_square = possible_squares[i];

            if (board.boards[board.color ^ 1, new_square] != StandartChess.no_piece)
            {
                movelist[moveIdx] = move_x_to_y(square, new_square, StandartChess.no_promotion);
                moveIdx++;
            }
        }
        return movelist;
    }
    public int[] king_move(int[] movelist, Position board, byte square)
    {
        //normal moves
        byte[] possible_squares = king_table[square];
        for (int i = 0; i < possible_squares.Length; i++)
        {
            byte new_square = possible_squares[i];
            if (board.boards[board.color, new_square] == StandartChess.no_piece)
            {
                movelist[moveIdx] = move_x_to_y(square, new_square, StandartChess.no_promotion);
                moveIdx++;
            }
        }

        //castling
        if (board.king_not_moved[board.color])
        {
            Debug.Assert(square == 60 || square == 4);
            //queenside castling
            if (board.rook_not_moved[(board.color << 1) ^ 0] && board.board[square - 1] == StandartChess.no_piece && board.board[square - 2] == StandartChess.no_piece && board.board[square - 3] == StandartChess.no_piece)
            {
                movelist[moveIdx] = move_x_to_y(square, (byte)(square - 2), StandartChess.castle_or_en_passent);
                moveIdx++;
            }
            //kingside castling
            if (board.rook_not_moved[(board.color << 1) ^ 1] && board.board[square + 1] == StandartChess.no_piece && board.board[square + 2] == StandartChess.no_piece)
            {
                movelist[moveIdx] = move_x_to_y(square, (byte)(square + 2), StandartChess.castle_or_en_passent);
                moveIdx++;
            }
        }

        return movelist;
    }
    public int[] king_capture(int[] movelist, Position board, byte square)
    {
        //normal moves
        byte[] possible_squares = king_table[square];
        for (int i = 0; i < possible_squares.Length; i++)
        {
            byte new_square = possible_squares[i];
            if (board.boards[board.color ^ 1, new_square] != StandartChess.no_piece)
            {
                movelist[moveIdx] = move_x_to_y(square, new_square, StandartChess.no_promotion);
                moveIdx++;
            }
        }

        return movelist;
    }
    public int[] sliding_piece_move(int[] movelist, Position board, byte square, byte[,][] array)
    {
        for (int i = 0; i < 4; i++)
        {
            byte[] possible_squares = array[square, i];

            for (int j = 0; j < possible_squares.Length; j++)
            {
                byte new_square = possible_squares[j];

                if (board.board[new_square] == StandartChess.no_piece)
                {
                    movelist[moveIdx] = move_x_to_y(square, new_square, StandartChess.no_promotion);
                    moveIdx++;
                }
                else
                {
                    if (board.boards[board.color, new_square] == StandartChess.no_piece)
                    {
                        movelist[moveIdx] = move_x_to_y(square, new_square, StandartChess.no_promotion);
                        moveIdx++;
                    }

                    break;
                }
            }
        }

        return movelist;
    }
    public int[] sliding_piece_capture(int[] movelist, Position board, byte square, byte[,][] array)
    {
        for (int i = 0; i < 4; i++)
        {
            byte[] possible_squares = array[square, i];

            for (int j = 0; j < possible_squares.Length; j++)
            {
                byte new_square = possible_squares[j];

                if(board.board[new_square] != StandartChess.no_piece)
                { 
                if (board.boards[board.color ^ 1, new_square] != StandartChess.no_piece)
                {
                    movelist[moveIdx] = move_x_to_y(square, new_square, StandartChess.no_promotion);
                    moveIdx++;
                }
                    break;
                }
            }
        }

        return movelist;
    }
    public void check_vector_for_check(Position board, byte square, byte[,][] array, int direction, byte piece_color, byte piece)
    {
        byte[] possible_squares = array[square, direction];

        for (int j = 0; j < possible_squares.Length; j++)
        {
            byte new_square = possible_squares[j];

            if (board.board[new_square] != StandartChess.no_piece)
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
class ReverseMove
{
    public byte enPassent = byte.MaxValue, fiftyMoveRule = 0, king_changes = byte.MaxValue, rook_changes = byte.MaxValue, moved_piece_idx = 0, removed_piece_idx = 0;
    public int[,] moved_pieces = new int[2, 2], removed_pieces = new int[2, 2];
}
