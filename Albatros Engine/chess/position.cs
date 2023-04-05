using System;

class Position
{
    //has every piece on it
    public byte[] board = new byte[64];

    public byte[] idx_board = new byte[64];

    //board indexed  by the color
    public byte[,] boards = new byte[2, 64];

    //0 is black and 1 is white
    public byte color = 0;

    //the square on wich the taking piece lands
    public byte en_passent_square = byte.MaxValue;

    //squares are on the first 6 bits
    //the array is indexed by the piece
    public byte[][] piece_square_lists = new byte[15][];

    //amount of  pieces in the piece square list indexed by piecetype
    public byte[] piececount = new byte[15];

    public byte fifty_move_rule = 0;

    //if the kings and rooks can castle
    //it is castle[color, king/queenside]
    public bool[] rook_not_moved = new bool[4];
    public bool[] king_not_moved = new bool[2];
    public Position()
    {
        int[] list_init = new int[] { 0, 8, 10, 10, 10, 9, 1 };
        for (int i = 0; i < 2; i++)
        {
            for (int j = 1; j < list_init.Length; j++)
                piece_square_lists[(i << 3) ^ j] = new byte[list_init[j]];
        }
    }
    public void reset()
    {
        board = new byte[64];
        boards = new byte[2, 64];
        piececount = new byte[15];
        king_not_moved = new bool[2];
        rook_not_moved = new bool[4];
    }
    public Position copy()
    {
        Position output = new Position();
        Array.Copy(board, output.board, board.Length);
        Array.Copy(boards, output.boards, boards.Length);
        Array.Copy(idx_board, output.idx_board, idx_board.Length);
        output.color = color;
        output.en_passent_square = en_passent_square;
        for (int i = 0; i < piece_square_lists.Length; i++)
        {
            if (piece_square_lists[i] != null)
            {
                output.piece_square_lists[i] = new byte[piece_square_lists[i].Length];
                Array.Copy(piece_square_lists[i], output.piece_square_lists[i], piece_square_lists[i].Length);
            }
        }
        Array.Copy(piececount, output.piececount, piececount.Length);
        output.fifty_move_rule = fifty_move_rule;
        Array.Copy(rook_not_moved, output.rook_not_moved, rook_not_moved.Length);
        Array.Copy(king_not_moved, output.king_not_moved, king_not_moved.Length);
        return output;
    }
}
