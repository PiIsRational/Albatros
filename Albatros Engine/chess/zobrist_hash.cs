using System;

class zobrist_hash
{
    standart_chess chess_stuff = new standart_chess();
    Random random = new Random(137305668);
    ulong[][] hash_tables = new ulong[15][];
    ulong white_to_play;
    ulong[] en_passent = new ulong[8], castling = new ulong[4];
    public zobrist_hash()
    {
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 7; j++)
            {
                for (int k = 0; k < 64; k++)
                {
                    if (hash_tables[(i << 3) ^ j] == null)
                        hash_tables[(i << 3) ^ j] = new ulong[64];

                    hash_tables[(i << 3) ^ j][k] = generate_random_ulong();
                }
            }
        }

        white_to_play = generate_random_ulong();

        for (int i = 0; i < en_passent.Length; i++)
            en_passent[i] = generate_random_ulong();

        for (int i = 0; i < castling.Length; i++)
            castling[i] = generate_random_ulong();
    }
    public ulong generate_random_ulong()
    {
        byte[] bytes = new byte[8];
        random.NextBytes(bytes);
        return BitConverter.ToUInt64(bytes);
    }
    public ulong hash_position(Position board)
    {
        ulong output;

        //color
        if (board.color == 1)
            output = white_to_play;
        else
            output = 0;

        //castling
        if (board.king_not_moved[0])
        {
            for (int i = 0; i < 2; i++)
                if (board.rook_not_moved[i])
                    output ^= castling[i];
        }

        if (board.king_not_moved[1])
        {
            for (int i = 2; i < 4; i++)
                if (board.rook_not_moved[i])
                    output ^= castling[i];
        }

        //en passent
        if (board.en_passent_square != 255)
            output ^= en_passent[chess_stuff.get_y_from_square(board.en_passent_square)];

        //finally go trough each piece in the piece list 
        for (int i = 0; i < 2; i++)
            for (int j = 0; j < 7; j++)
                for (int k = 0; k < board.piececount[(i << 3) ^ j]; k++)
                    output ^= hash_tables[(i << 3) ^ j][board.piece_square_lists[(i << 3) ^ j][k]];

        return output;
    }
    public ulong UpdateHashAfterMove(Position board, ReverseMove unmake_move, ulong hash)
    {
        if (unmake_move.king_changes != byte.MaxValue || unmake_move.rook_changes != byte.MaxValue)
            return hash_position(board);

        //change the color
        hash ^= white_to_play;

        //remove pieces that are no longer on the board
        for (int i = 0; i < unmake_move.removed_piece_idx; i++)
            hash ^= hash_tables[unmake_move.removed_pieces[i, 1]][unmake_move.removed_pieces[i, 0]];

        for (int i = 0; i < unmake_move.moved_piece_idx; i++)
        {
            //remove pieces that have moved
            hash ^= hash_tables[board.board[unmake_move.moved_pieces[i, 1]]][unmake_move.moved_pieces[i, 0]];

            //add pieces that have moved
            hash ^= hash_tables[board.board[unmake_move.moved_pieces[i, 1]]][unmake_move.moved_pieces[i, 1]];
        }

        //remove the old en passent square
        if(unmake_move.en_passent != byte.MaxValue)
            hash ^= en_passent[chess_stuff.get_y_from_square(unmake_move.en_passent)];

        //add the new en passent square
        if (board.en_passent_square != byte.MaxValue)
            hash ^= en_passent[chess_stuff.get_y_from_square(board.en_passent_square)];

        return hash;
    }
    public ulong update_null_move_hash(ulong hash, ReverseMove unmake_move)
    {
        //change the color
        hash ^= white_to_play;

        //remove the old en passent square
        if (unmake_move.en_passent != byte.MaxValue)
            hash ^= en_passent[chess_stuff.get_y_from_square(unmake_move.en_passent)];

        return hash;
    }
}