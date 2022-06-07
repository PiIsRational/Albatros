using System;
using System.Collections.Generic;
public class MoveGen
{
    /*
    * Pieces:
    * Encoded in 5-bits(variable Byte)
    * <================>
    * NoPiece:             0b00000000
    * 
    * Black:
    * 
    * PawnStart:           0b00000001
    * PawnEnPassent:       0b00000010
    * PawnNormal:          0b00000011
    * Knight:              0b00000100
    * Bishop:              0b00000101
    * KingCanCastle:       0b00000110
    * King:                0b00000111
    * Queen:               0b00001000
    * RookCanCastle:       0b00001001
    * Rook:                0b00001010
    * 
    * White:
    * 
    * PawnStart:           0b00010001
    * PawnEnPassent:       0b00010010
    * PawnNormal           0b00010011
    * Knight:              0b00010100
    * Bishop:              0b00010101
    * KingCanCastle:       0b00010110
    * King:                0b00010111
    * Queen:               0b00011000
    * RookCanCastle:       0b00011001
    * Rook:                0b00011010
    * 
    * Board:
    * Byte[,] = new Byte[9,9]
    * Mooves:
    * PositionY = 8 // Queening
    * PositionY = 1 // Queening
    * 
    * If Moove Format is int[3] , or int[4] then special Moove involving two pieces
    * Unmove has the format X_a , Y_a , Piece_a , X_b , Y_b , Piece_b
    */
    public int[] UnmakeMove, EnPassent = new int[2];
    bool WrongPosition = false;
    public bool non_pawn_material = false;
    int[] kingpositions = new int[4];
    public int fifty_move_rule = 0;
    List<int[]> MoveList = new List<int[]>();
    List<int[]>[][,] Move_lookup = new List<int[]>[2][,];
    public byte[,] UndoMove(byte[,] Position, int[] MoveUndo)
    {
        if ((MoveUndo.Length / 3) * 3 == MoveUndo.Length)
        {
            for (int i = 0; i < MoveUndo.Length / 3; i++)
                Position[MoveUndo[i * 3], MoveUndo[i * 3 + 1]] = (byte)MoveUndo[i * 3 + 2];
        }
        else
            Console.WriteLine("Error");
        return Position;
    }
    public int Mate(byte[,] board, byte Color)
    {
        //Look for blocking of Position
        List<int[]> Moves = ReturnPossibleMoves(board, Color);

        if (Moves == null)
            return 2;

        byte[,] MoveUndo = new byte[9, 9];
        byte NewColor = (byte)(1 - Color);

        foreach (int[] Move in Moves)
        {
            Array.Copy(board, MoveUndo, MoveUndo.Length);
            board = PlayMove(board, Color, Move);

            if (!CompleteCheck(board, NewColor))
            {
                Array.Copy(MoveUndo, board, board.Length);
                return 2;
            }
            else
                Array.Copy(MoveUndo, board, board.Length);
        }

        //Mate or Stalemate
        //Case: Mate
        if (CompleteCheck(board, NewColor))
            return -1;
        //Case: Stalemate
        else
            return 0;
    }
    public int[]FindKings(byte[,] InputBoard)
    {
        int[] Output = new int[4];
        for (int i = 1; i < 9; i++)
        {
            for (int j = 1; j < 9; j++)
            {
                switch(InputBoard[i,j])
                {
                    case 0b00000110:
                        Output[0] = i;
                        Output[1] = j;
                        break;
                    case 0b00000111:
                        Output[0] = i;
                        Output[1] = j;
                        break;
                    case 0b00010110:
                        Output[2] = i;
                        Output[3] = j;
                        break;
                    case 0b00010111:
                        Output[2] = i;
                        Output[3] = j;
                        break;
                }
            }
        }
        return Output;
    }
    public byte[,] PlayMove(byte[,] InputBoard, byte color, int[] move)
    {
        int k = move[0];
        int j = move[1];
        int[] CurrentMoove;

        if (move.Length == 4)
            CurrentMoove = new int[2] { move[2], move[3] };
        else if (move.Length == 5)
            CurrentMoove = new int[3] { move[2], move[3], move[4] };
        else
            CurrentMoove = new int[0];

        switch (InputBoard[k, j] & 0b00001111)
        {
            //PawnStart
            case 0b00000001:
                fifty_move_rule = 0;
                Array.Copy(PawnStartExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //NormalPawn
            case 0b00000011:
                fifty_move_rule = 0;
                Array.Copy(PawnDirectExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //Knight
            case 0b00000100:
                fifty_move_rule++;
                Array.Copy(NormalExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //Bishop
            case 0b00000101:
                fifty_move_rule++;
                Array.Copy(NormalExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //King Can Castle
            case 0b00000110:
                fifty_move_rule++;
                Array.Copy(KingCastleExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //Normal King
            case 0b00000111:
                fifty_move_rule++;
                Array.Copy(NormalExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //Queen
            case 0b00001000:
                fifty_move_rule++;
                Array.Copy(NormalExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //RookCanCastle
            case 0b00001001:
                fifty_move_rule++;
                Array.Copy(StartExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //Normal Rook
            case 0b00001010:
                fifty_move_rule++;
                Array.Copy(NormalExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
        }

        if (UnmakeMove[2] != 0 && UnmakeMove[5] != 0)
            fifty_move_rule = 0;

        for (int x = 1; x < 9; x++)
        {
            int y = 4 + color;

            if (InputBoard[x, y] != 0 && InputBoard[x, y] >> 4 == (1 - color) && (InputBoard[x, y] & 0b00001111) == 0b10)
            {
                //create a copy of the move
                int[] Copy = new int[UnmakeMove.Length];
                Array.Copy(UnmakeMove, Copy, Copy.Length);
                //make the array larger
                UnmakeMove = new int[UnmakeMove.Length + 3];
                //copy the copy into the move
                Array.Copy(Copy, 0, UnmakeMove, 0, Copy.Length);
                //create the additive to the move undo
                Copy = new int[] { x, y, InputBoard[x, y] };
                //copy it to the unmake move 
                Array.Copy(Copy, 0, UnmakeMove, UnmakeMove.Length - 3, Copy.Length);
                //increment the piece
                InputBoard[x, y]++;
                break;
            }
        }

        return InputBoard;
    }
    public byte[,] PawnDirectExecuteMooves(byte[,] InputBoard, int[] Move, int X, int Y)
    {
        byte Copy = InputBoard[X, Y];

        //Queening or en passent
        if (Move.Length == 3)
        {
            switch (Move[2])
            {
                // EnPassent
                case 0:
                    UnmakeMove = new int[] { X, Y, InputBoard[X, Y], Move[0], Move[1], 0, Move[0], Y, InputBoard[Move[0], Y] };
                    InputBoard[Move[0], Y] = 0;
                    break;
                case 1:
                    //Knight
                    Copy++;
                    UnmakeMove = new int[] { X, Y, InputBoard[X, Y], Move[0], Move[1], InputBoard[Move[0], Move[1]] };
                    break;
                case 2:
                    //Bishop
                    Copy += 2;
                    UnmakeMove = new int[] { X, Y, InputBoard[X, Y], Move[0], Move[1], InputBoard[Move[0], Move[1]] };
                    break;
                case 3:
                    //Queen
                    Copy += 5;
                    UnmakeMove = new int[] { X, Y, InputBoard[X, Y], Move[0], Move[1], InputBoard[Move[0], Move[1]] };
                    break;
                case 4:
                    //Rook
                    Copy += 7;
                    UnmakeMove = new int[] { X, Y, InputBoard[X, Y], Move[0], Move[1], InputBoard[Move[0], Move[1]] };
                    break;
            }
        }
        else
        {
            //en passent
            if (Move[0] != X && InputBoard[Move[0], Move[1]] == 0)
            {
                UnmakeMove = new int[] { X, Y, InputBoard[X, Y], Move[0], Move[1], 0, Move[0], Y, InputBoard[Move[0], Y] };
                InputBoard[Move[0], Y] = 0;
            }
            else
                UnmakeMove = new int[] { X, Y, Copy, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
        }
        InputBoard[X, Y] = 0;
        InputBoard[Move[0], Move[1]] = Copy;

        return InputBoard;
    }
    public byte[,] KingCastleExecuteMooves(byte[,] InputBoard, int[] Move, int X, int Y)
    {
        byte KingColor = (byte)(InputBoard[X, Y] >> 4);
        byte Copy = (byte)(InputBoard[X, Y] + 1);

        if (Move.Length == 2 && Move[0] != X + 2 && Move[0] != X - 2) 
        {
            UnmakeMove = new int[] { X, Y, Copy - 1, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
            InputBoard[X, Y] = 0;
            InputBoard[Move[0], Move[1]] = Copy;
        }
        // if Castelling
        else
        {
            byte EnemyColor = 0;
            if (KingColor == 0)
                EnemyColor = 1;
            //Copy the King to two new Squares
            InputBoard[(X + Move[0]) / 2, Y] = Copy;
            InputBoard[Move[0], Move[1]] = Copy;
            //Check if any of the Kings are in Check
            if (!CompleteCheck(InputBoard, EnemyColor))
            {
                //Old King Position = 0
                InputBoard[X, Y] = 0;
                //QueenSideCastle
                if (X - Move[0] == 2)
                {
                    UnmakeMove = new int[] { X, Y, Copy - 1, Move[0], Move[1], 0, X - 1, Y, 0, 1, Y, InputBoard[1, Y] };
                    InputBoard[X - 1, Y] = (byte)(InputBoard[1, Y] + 1);
                    InputBoard[1, Y] = 0;
                }
                //KingSideCastle
                else
                {
                    UnmakeMove = new int[] { X, Y, Copy - 1, Move[0], Move[1], 0, X + 1, Y, 0, 8, Y, InputBoard[8, Y] };
                    InputBoard[X + 1, Y] = (byte)(InputBoard[8, Y] + 1);
                    InputBoard[8, Y] = 0;
                }
            }
        }
        return InputBoard;
    }
    public byte[,] StartExecuteMooves(byte[,] InputBoard, int[] Move, int X, int Y)
    {
        byte Copy = (byte)(InputBoard[X, Y] + 1);

        UnmakeMove = new int[] { X, Y, Copy - 1, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
        InputBoard[X, Y] = 0;
        InputBoard[Move[0], Move[1]] = Copy;

        return InputBoard;
    }
    public byte[,] PawnStartExecuteMooves(byte[,] InputBoard, int[] Move, int X, int Y)
    {
        byte Copy = (byte)(InputBoard[X, Y] + 2);
        InputBoard[X, Y] = 0;
        if (Move[1] == Y + 2 || Move[1] == Y - 2)
        {
            UnmakeMove = new int[] { X, Y, Copy - 2, Move[0], Move[1], 0 };
            InputBoard[Move[0], Move[1]] = (byte)(Copy - 1);

            EnPassent[0] = Move[0];
            EnPassent[1] = Move[1];
        }
        else
        {
            UnmakeMove = new int[] { X, Y, Copy - 2, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
            InputBoard[Move[0], Move[1]] = Copy;
        }

        return InputBoard;
    }
    public byte[,] NormalExecuteMooves(byte[,] InputBoard, int[] Move, int X, int Y)
    {
        byte Copy = InputBoard[X, Y];
        UnmakeMove = new int[] { X, Y, Copy, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
        InputBoard[X, Y] = 0;
        InputBoard[Move[0], Move[1]] = Copy;
        return InputBoard;
    }
    public void reset_move_lookup(List<int[]> MoveList, byte color, byte[,] board)
    {
        //Reset the move lookup table
        Move_lookup[color] = new List<int[]>[9, 9];
        for (int i = 0; i < 9; i++)
            for (int j = 0; j < 9; j++)
                Move_lookup[color][i, j] = new List<int[]>();

        foreach (int[] Move in MoveList) 
        {
            switch (board[Move[0], Move[1]] - (board[Move[0], Move[1]] >> 4) * 0b10000)
            {
                //normal pawn
                case 0b00000011:

                //king which can caste
                case 0b00000110:
                    for (int i = 1; i < 9; i++)
                        Move_lookup[color][i, Move[1]].Add(new int[] { Move[0], Move[1] });

                    Move_lookup[color][Move[2], Move[3]].Add(new int[] { Move[0], Move[1] });
                    break;
                //normal moves
                default:
                    Move_lookup[color][Move[2], Move[3]].Add(new int[] { Move[0], Move[1] });
                    break;
            }

        }
    }
    public List<int[]> ReturnPossibleMoves(byte[,] board, byte color)
    {
        List<int[]> Output = new List<int[]>();
        non_pawn_material = false;
        for (int i = 1; i < 9; i++)
        {
            for (int j = 1; j < 9; j++)
            {
                if (board[i, j] != 0 && board[i, j] >> 4 == color)
                {
                    switch (board[i, j] & 0b00001111)
                    {
                        case 0b00000001:
                            Output = pawn_move(Output, board, i, j, color);
                            break;
                        case 0b00000011:
                            Output = pawn_move(Output, board, i, j, color);
                            break;
                        case 0b00000100:
                            non_pawn_material = true;
                            Output = knight_move(Output, board, i, j, color);
                            break;
                        case 0b00000101:
                            non_pawn_material = true;
                            Output = bishop_move(Output, board, i, j, color);
                            break;
                        case 0b00000110:
                            Output = king_move(Output, board, i, j, color);
                            break;
                        case 0b00000111:
                            Output = king_move(Output, board, i, j, color);
                            break;
                        case 0b00001000:
                            non_pawn_material = true;
                            Output = queen_move(Output, board, i, j, color);
                            break;
                        case 0b00001001:
                            non_pawn_material = true;
                            Output = rook_move(Output, board, i, j, color);
                            break;
                        case 0b00001010:
                            non_pawn_material = true;
                            Output = rook_move(Output, board, i, j, color);
                            break;

                    }
                    if (WrongPosition)
                    {
                        WrongPosition = false;
                        return null;
                    }
                }
            }
        }
        return Output;
    }
    public List<int[]> ReturnPossibleCaptures(byte[,] InputBoard, byte color)
    {
        List<int[]> Output = new List<int[]>();

        for (int i = 1; i < 9; i++)
        {
            for (int j = 1; j < 9; j++)
            {
                if (InputBoard[i, j] != 0 && InputBoard[i, j] >> 4 == color)
                {
                    switch (InputBoard[i, j] & 0b00001111)
                    {
                        case 0b00000001:
                            Output = PawnCaptures(Output,InputBoard, i, j, color);
                            break;
                        case 0b00000011:
                            Output = PawnCaptures(Output, InputBoard, i, j, color);
                            break;
                        case 0b00000100:
                            Output = KnightCaptures(Output, InputBoard, i, j, color);
                            break;
                        case 0b00000101:
                            Output = BishopCaptures(Output, InputBoard, i, j, color);
                            break;
                        case 0b00000110:
                            Output = KingCaptures(Output, InputBoard, i, j, color);
                            break;
                        case 0b00000111:
                            Output = KingCaptures(Output, InputBoard, i, j, color);
                            break;
                        case 0b00001000:
                            Output = QueenCaptures(Output, InputBoard, i, j, color);
                            break;
                        case 0b00001001:
                            Output = RookCaptures(Output, InputBoard, i, j, color);
                            break;
                        case 0b00001010:
                            Output = RookCaptures(Output, InputBoard, i, j, color);
                            break;

                    }
                    if (WrongPosition)
                    {
                        WrongPosition = false;
                        return null;
                    }
                }
            }
        }
        return Output;
    }
    public void check(byte[,] InputBoard, int X, int Y)
    {
        WrongPosition = ((InputBoard[X, Y] & 0b00001110) == 0b110) || WrongPosition;
    }
    public bool CompleteCheck(byte[,] InputBoard, byte color)
    {
        for (int i = 1; i < 9; i++)
        {
            for (int j = 1; j < 9; j++)
            {
                if (InputBoard[i, j] != 0 && InputBoard[i, j] >> 4 == color)
                {
                    switch (InputBoard[i, j] & 0b00001111)
                    {
                        case 0b00000001:
                            PawnCheck(InputBoard, i, j, color);
                            break;
                        case 0b00000011:
                            PawnCheck(InputBoard, i, j, color);
                            break;
                        case 0b00000100:
                            KnightCheck(InputBoard, i, j, color);
                            break;
                        case 0b00000101:
                            BishopCheck(InputBoard, i, j, color);
                            break;
                        case 0b00000110:
                            KingCheck(InputBoard, i, j, color);
                            break;
                        case 0b00000111:
                            KingCheck(InputBoard, i, j, color);
                            break;
                        case 0b00001000:
                            QueenCheck(InputBoard, i, j, color);
                            break;
                        case 0b00001001:
                            RookCheck(InputBoard, i, j, color);
                            break;
                        case 0b00001010:
                            RookCheck(InputBoard, i, j, color);
                            break;
                    }
                    if (WrongPosition)
                    {
                        WrongPosition = false;
                        return true;
                    }
                }
            }
        }
        return false;
    }
    public bool CastlingCheck(byte[,] InputBoard, int[] move)
    {
        byte Copy = (byte)(InputBoard[move[0], move[1]]);
        if ((Copy & 0b00001111) != 0b00000110) return false;

        byte[,] CurrentBoard = new byte[9, 9];

        Array.Copy(InputBoard, 0, CurrentBoard, 0, InputBoard.Length);

        if (move.Length == 5)
        {
            byte KingColor = (byte)(InputBoard[move[0], move[1]] >> 4);
            byte EnemyColor = (byte)(1 - KingColor);

            //Copy the King to two new Squares
            CurrentBoard[(move[0] + move[2]) >> 1, move[1]] = Copy;
            CurrentBoard[move[2], move[3]] = Copy;

            //Check if any of the Kings are in Check
            if (CompleteCheck(CurrentBoard, EnemyColor))
                return true;
        }
        return false;
    }
    public int[] make_normal_move(byte[,] board, int x, int y, int new_x, int new_y)
    {
        check(board, new_x, new_y);
        return new int[4] { x, y, new_x, new_y };
    }
    public int[] make_quiet_move(int x, int y, int new_x, int new_y)
    {
        return new int[4] { x, y, new_x, new_y };
    }
    public int[] make_non_normal_quiet_move(int x, int y, int new_x, int new_y, int move_index)
    {
        return new int[5] { x, y, new_x, new_y, move_index };
    }
    public int[] make_non_normal_move(byte[,] board, int x, int y, int new_x, int new_y, int move_index)
    {
        check(board, new_x, new_y);
        return new int[5] { x, y, new_x, new_y, move_index };
    }
    public List<int[]> make_normal_pawn_moves(List<int[]> output, byte[,] board, int x, int y, int new_x, int new_y)
    {
        check(board, new_x, new_y);
        if (!WrongPosition)
        {
            if (new_y == 1 || new_y == 8)
                for (int i = 1; i <= 4; i++)
                    output.Add(make_non_normal_quiet_move(x, y, new_x, new_y, i));
            else
                output.Add(make_quiet_move(x, y, new_x, new_y));
        }
        return output;
    }
    public List<int[]> make_quiet_pawn_moves(List<int[]> output, byte[,] board, int x, int y, int new_x, int new_y)
    {
        if (new_y == 1 || new_y == 8)
            for (int i = 1; i <= 4; i++)
                output.Add(make_non_normal_quiet_move(x, y, new_x, new_y, i));
        else
            output.Add(make_quiet_move(x, y, new_x, new_y));

        return output;
    }
    public List<int[]> knight_move(List<int[]> Output, byte[,] board, int current_x, int current_y, byte color)
    {
        int new_x = current_x + 2 , new_y = 0;
        if (new_x <= 8)
        {
            new_y = current_y - 1;
            if (new_y >= 1 && (board[new_x, new_y] == 0 || board[new_x, new_y] >> 4 != color))
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));

            new_y = current_y + 1;
            if (new_y <= 8 && (board[new_x, new_y] == 0 || board[new_x, new_y] >> 4 != color))
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
        }
        new_x -= 4;
        if (new_x >= 1)
        {
            new_y = current_y + 1;
            if (new_y <= 8 && (board[new_x, new_y] == 0 || board[new_x, new_y] >> 4 != color))
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));

            new_y = current_y - 1;
            if (new_y >= 1 && (board[new_x, new_y] == 0 || board[new_x, new_y] >> 4 != color))
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
        }
        new_x += 3;
        if (new_x <= 8)
        {
            new_y = current_y - 2;
            if (new_y >= 1 && (board[new_x, new_y] == 0 || board[new_x, new_y] >> 4 != color))
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));

            new_y = current_y + 2;
            if (new_y <= 8 && (board[new_x, new_y] == 0 || board[new_x, new_y] >> 4 != color))
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
        }
        new_x -= 2;
        if (new_x >= 1)
        {
            new_y = current_y + 2;
            if (new_y <= 8 && (board[new_x, new_y] == 0 || board[new_x, new_y] >> 4 != color))
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));

            new_y = current_y - 2;
            if (new_y >= 1 && (board[new_x, new_y] == 0 || board[new_x, new_y] >> 4 != color))
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
        }
        return Output;
    }
    public List<int[]> bishop_move(List<int[]> Output, byte[,] board, int current_x, int current_y, byte color)
    {
        int new_x, new_y;

        for (int i = 1; i + current_x <= 8 && i + current_y <= 8; i++)
        {
            new_x = current_x + i;
            new_y = current_y + i;

            if (board[new_x, new_y] == 0)
                Output.Add(make_quiet_move(current_x, current_y, new_x, new_y));

            else
            {
                if (board[new_x, new_y] >> 4 != color)
                {
                    Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
                    break;
                }
                else
                    break;
            }
        }

        for (int i = 1; current_x - i >= 1 && current_y - i >= 1; i++)
        {
            new_x = current_x - i;
            new_y = current_y - i;

            if (board[new_x, new_y] == 0)
                Output.Add(make_quiet_move(current_x, current_y, new_x, new_y));

            else
            {
                if (board[new_x, new_y] >> 4 != color)
                {
                    Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
                    break;
                }
                else
                    break;
            }
        }

        for (int i = 1; current_x - i >= 1 && i + current_y <= 8; i++)
        {
            new_x = current_x - i;
            new_y = current_y + i;

            if (board[new_x, new_y] == 0)
                Output.Add(make_quiet_move(current_x, current_y, new_x, new_y));

            else
            {
                if (board[new_x, new_y] >> 4 != color)
                {
                    Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
                    break;
                }
                else
                    break;
            }
        }

        for (int i = 1; i + current_x <= 8 && current_y - i >= 1; i++)
        {
            new_x = current_x + i;
            new_y = current_y - i;

            if (board[new_x, new_y] == 0)
                Output.Add(make_quiet_move(current_x, current_y, new_x, new_y));

            else
            {
                if (board[new_x, new_y] >> 4 != color)
                {
                    Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
                    break;
                }
                else
                    break;
            }
        }

        return Output;
    }
    public List<int[]> rook_move(List<int[]> Output,byte[,] board, int current_x, int current_y, byte color)
    {

        for (int i = current_x + 1; i <= 8; i++)
        {
            if (board[i, current_y] == 0)
                Output.Add(make_quiet_move(current_x, current_y, i, current_y));

            else
            {
                if (board[i, current_y] >> 4 != color)
                {
                    Output.Add(make_normal_move(board, current_x, current_y, i, current_y));
                    break;
                }
                else
                    break;
            }
        }

        for (int i = current_x - 1; i >= 1; i--)
        {
            if (board[i, current_y] == 0)
                Output.Add(make_quiet_move(current_x, current_y, i, current_y));

            else
            {
                if (board[i, current_y] >> 4 != color)
                {
                    Output.Add(make_normal_move(board, current_x, current_y, i, current_y));
                    break;
                }
                else
                    break;
            }
        }

        for (int i = current_y + 1; i <= 8; i++)
        {
            if (board[current_x, i] == 0)
                Output.Add(make_quiet_move(current_x, current_y, current_x, i));

            else
            {
                if (board[current_x, i] >> 4 != color)
                {
                    Output.Add(make_normal_move(board, current_x, current_y, current_x, i));
                    break;
                }
                else
                    break;
            }
        }

        for (int i = current_y - 1; i >= 1; i--)
        {
            if (board[current_x, i] == 0)
                Output.Add(make_quiet_move(current_x, current_y, current_x, i));

            else
            {
                if (board[current_x, i] >> 4 != color)
                {
                    Output.Add(make_normal_move(board, current_x, current_y, current_x, i));
                    break;
                }
                else
                    break;
            }
        }

        return Output;
    }
    public List<int[]> queen_move(List<int[]> Output, byte[,] board, int current_x, int current_y, byte color)
    {
        Output = bishop_move(Output, board, current_x, current_y, color);
        Output = rook_move(Output, board, current_x, current_y, color);

        return Output;
    }
    public List<int[]> king_move(List<int[]> Output, byte[,] board, int current_x, int current_y, byte color)
    {
        int new_x = current_x + 2, new_y = current_y;

        //Casteling
        if ((board[current_x, current_y] & 0b00001111) == 0b00000110 && board[current_x + 1, new_y] == 0 && board[new_x, new_y] == 0 && board[current_x + 3, new_y] != 0 &&
            (board[current_x + 3, new_y] & 0b00001111) == 0b00001001 && !CastlingCheck(board, make_non_normal_quiet_move(current_x, current_y, new_x, new_y, 0)))
            Output.Add(make_non_normal_quiet_move(current_x, current_y, new_x, new_y, 0));

        new_x = current_x - 2;
        if ((board[current_x, current_y] & 0b00001111) == 0b00000110 && board[current_x - 1, new_y] == 0 && board[new_x, new_y] == 0 && board[current_x - 3, new_y] == 0 &&
            board[current_x - 4, new_y] != 0 && (board[current_x - 4, new_y] & 0b00001111) == 0b00001001 && !CastlingCheck(board, make_non_normal_quiet_move(current_x, current_y, new_x, new_y, 0)))
            Output.Add(make_non_normal_quiet_move(current_x, current_y, new_x, new_y, 0));

        // normal Mooves
        new_x += 1;
        if (new_x >= 1)
        {
            if (board[new_x, new_y] == 0 || board[new_x, new_y] >> 4 != color) 
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));

            new_y = current_y + 1;
            if (new_y <= 8 && (board[new_x, new_y] == 0 || board[new_x, new_y] >> 4 != color))
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));

            new_y = current_y - 1;
            if (new_y >= 1 && (board[new_x, new_y] == 0 || board[new_x, new_y] >> 4 != color))
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
        }
        new_x += 2;
        if (new_x <= 8)
        {
            new_y = current_y;
            if (board[new_x, new_y] == 0 || board[new_x, new_y] >> 4 != color)
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));

            new_y = current_y + 1;
            if (new_y <= 8 && (board[new_x, new_y] == 0 || board[new_x, new_y] >> 4 != color))
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));

            new_y = current_y - 1;
            if (new_y >= 1 && (board[new_x, new_y] == 0 || board[new_x, new_y] >> 4 != color))
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
        }

        new_x = current_x;
        new_y = current_y + 1;
        if (new_y <= 8 && (board[new_x, new_y] == 0 || board[new_x, new_y] >> 4 != color))
            Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));

        new_y = current_y - 1;
        if (new_y >= 1 && (board[new_x, new_y] == 0 || board[new_x, new_y] >> 4 != color))
            Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));

        return Output;
    }
    public List<int[]> pawn_move(List<int[]> Output, byte[,] board, int current_x, int current_y, byte color)
    {
        int colorminus = 2 * color - 1;
        int new_x = current_x, new_y = current_y + 2 * colorminus;

        if ((board[current_x, current_y] & 0b00001111) == 0b1 && board[new_x, current_y + colorminus] == 0 && board[new_x, new_y] == 0)
            Output.Add(make_quiet_move(current_x, current_y, new_x, new_y));

        new_y -= colorminus;
        if (board[new_x, new_y] == 0)
            Output = make_quiet_pawn_moves(Output, board, current_x, current_y, new_x, new_y);


        new_x = current_x + 1;
        if (new_x <= 8)
        {
            if (board[new_x, new_y] != 0 && board[new_x, new_y] >> 4 != color)
                Output = make_normal_pawn_moves(Output, board, current_x, current_y, new_x, new_y);

            else if (current_y == 4 + color && board[new_x, current_y] != 0 && board[new_x, current_y] >> 4 != color && (board[new_x, current_y] & 0b00001111) == 0b00000010)
                Output.Add(make_non_normal_move(board, current_x, current_y, new_x, new_y, 0));
        }

        new_x = current_x - 1;
        if (new_x >= 1)
        {
            if (board[new_x, new_y] != 0 && board[new_x, new_y] >> 4 != color)
                Output = make_normal_pawn_moves(Output, board, current_x, current_y, new_x, new_y);

            else if (current_y == 4 + color && board[new_x, current_y] != 0 && board[new_x, current_y] >> 4 != color && (board[new_x, current_y] & 0b00001111) == 0b00000010)
                Output.Add(make_non_normal_move(board, current_x, current_y, new_x, new_y, 0));
        }
        return Output;
    }
    public List<int[]> KnightCaptures(List<int[]> Output, byte[,] board, int current_x, int current_y, byte color)
    {
        int new_x = current_x + 2, new_y = 0;

        if (new_x <= 8)
        {
            new_y = current_y - 1;
            if (new_y >= 1 && board[new_x, new_y] != 0 && board[new_x, new_y] >> 4 != color)
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));

            new_y = current_y + 1;
            if (new_y <= 8 && board[new_x, new_y] != 0 && board[new_x, new_y] >> 4 != color)
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
        }

        new_x = current_x - 2;
        if (new_x >= 1)
        {
            new_y = current_y - 1;
            if (new_y >= 1 && board[new_x, new_y] != 0 && board[new_x, new_y] >> 4 != color)
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));

            new_y = current_y + 1;
            if (new_y <= 8 && board[new_x, new_y] != 0 && board[new_x, new_y] >> 4 != color)
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
        }

        new_x = current_x + 1;
        if (new_x <= 8)
        {
            new_y = current_y - 2;
            if (new_y >= 1 && board[new_x, new_y] != 0 && board[new_x, new_y] >> 4 != color)
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));

            new_y = current_y + 2;
            if (new_y <= 8 && board[new_x, new_y] != 0 && board[new_x, new_y] >> 4 != color)
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
        }

        new_x = current_x - 1;
        if (new_x <= 8)
        {
            new_y = current_y - 2;
            if (new_y >= 1 && board[new_x, new_y] != 0 && board[new_x, new_y] >> 4 != color)
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));

            new_y = current_y + 2;
            if (new_y <= 8 && board[new_x, new_y] != 0 && board[new_x, new_y] >> 4 != color)
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
        }
        return Output;
    }
    public List<int[]> BishopCaptures(List<int[]> Output, byte[,] board, int current_x, int current_y, byte color)
    {
        int new_x = 0, new_y = 0;
        for (int i = 1; i + current_x <= 8 && i + current_y <= 8; i++)
        {
            new_x = current_x + i;
            new_y = current_y + i;

            if (board[new_x, new_y] != 0)
            {
                if (board[new_x, new_y] >> 4 != color)
                {
                    Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
                    break;
                }
                else
                    break;
            }
        }

        for (int i = 1; current_x - i >= 1 && current_y - i >= 1; i++)
        {
            new_x = current_x - i;
            new_y = current_y - i;

            if (board[new_x, new_y] != 0)
            {
                if (board[new_x, new_y] >> 4 != color)
                {
                    Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
                    break;
                }
                else
                    break;
            }
        }

        for (int i = 1; current_x - i >= 1 && i + current_y <= 8; i++)
        {
            new_x = current_x - i;
            new_y = current_y + i;

            if (board[new_x, new_y] != 0)
            {
                if (board[new_x, new_y] >> 4 != color)
                {
                    Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
                    break;
                }
                else
                    break;
            }
        }

        for (int i = 1; i + current_x <= 8 && current_y - i >= 1; i++)
        {
            new_x = current_x + i;
            new_y = current_y - i;

            if (board[new_x, new_y] != 0)
            {
                if (board[new_x, new_y] >> 4 != color)
                {
                    Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
                    break;
                }
                else
                    break;
            }
        }

        return Output;
    }
    public List<int[]> RookCaptures(List<int[]> Output, byte[,] board, int current_x, int current_y, byte color)
    {
        for (int i = current_x + 1; i <= 8; i++)
        {
            if (board[i, current_y] != 0)
            {
                if (board[i, current_y] >> 4 != color)
                {
                    Output.Add(make_normal_move(board, current_x, current_y, i, current_y));
                    break;
                }
                else
                    break;
            }
        }

        for (int i = current_x - 1; i >= 1; i--)
        {
            if (board[i, current_y] != 0)
            {
                if (board[i, current_y] >> 4 != color)
                {
                    Output.Add(make_normal_move(board, current_x, current_y, i, current_y));
                    break;
                }
                else
                    break;
            }
        }

        for (int i = current_y + 1; i <= 8; i++)
        {
            if (board[current_x, i] != 0)
            {
                if (board[current_x, i] >> 4 != color)
                {
                    Output.Add(make_normal_move(board, current_x, current_y, current_x, i));
                    break;
                }
                else
                    break;
            }
        }

        for (int i = current_y - 1; i >= 1; i--)
        {
            if (board[current_x, i] != 0)
            {
                if (board[current_x, i] >> 4 != color)
                {
                    Output.Add(make_normal_move(board, current_x, current_y, current_x, i));
                    break;
                }
                else
                    break;
            }
        }

        return Output;

    }
    public List<int[]> QueenCaptures(List<int[]> Output, byte[,] board, int current_x, int current_y, byte color)
    {
        Output = RookCaptures(Output, board, current_x, current_y, color);
        Output = BishopCaptures(Output, board, current_x, current_y, color);
        return Output;
    }
    public List<int[]> KingCaptures(List<int[]> Output, byte[,] board, int current_x, int current_y, byte color)
    {
        int new_x = current_x - 1, new_y;

        if (new_x >= 1)
        {
            if (board[new_x, current_y] != 0 && board[new_x, current_y] >> 4 != color)
                Output.Add(make_normal_move(board, current_x, current_y, new_x, current_y));

            new_y = current_y + 1;
            if (new_y <= 8 && board[new_x, new_y] != 0 && board[new_x, new_y] >> 4 != color)
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));

            new_y = current_y - 1;
            if (new_y >= 1 && board[new_x, new_y] != 0 && board[new_x, new_y] >> 4 != color)
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
        }

        new_x = current_x + 1;
        if (new_x <= 8)
        {
            if (board[new_x, current_y] != 0 && board[new_x, current_y] >> 4 != color)
                Output.Add(make_normal_move(board, current_x, current_y, new_x, current_y));

            new_y = current_y + 1;
            if (new_y <= 8 && board[new_x, new_y] != 0 && board[new_x, new_y] >> 4 != color)
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));

            new_y = current_y - 1;
            if (new_y >= 1 && board[new_x, new_y] != 0 && board[new_x, new_y] >> 4 != color)
                Output.Add(make_normal_move(board, current_x, current_y, new_x, new_y));
        }

        new_y = current_y + 1;
        if (new_y <= 8 && board[current_x, new_y] != 0 && board[current_x, new_y] >> 4 != color)
            Output.Add(make_normal_move(board, current_x, current_y, current_x, new_y));

        new_y = current_y - 1;
        if (new_y >= 1 && board[current_x, new_y] != 0 && board[current_x, new_y] >> 4 != color)
            Output.Add(make_normal_move(board, current_x, current_y, current_x, new_y));

        return Output;
    }
    public List<int[]> PawnCaptures(List<int[]> Output, byte[,] board, int current_x, int current_y, byte color)
    {
        int colorminus = 2 * color - 1, new_x, new_y = current_y + colorminus;

        new_x = current_x + 1;
        if (new_x <= 8)
        {
            if (board[new_x, new_y] != 0 && board[new_x, new_y] >> 4 != color)
                Output = make_normal_pawn_moves(Output, board, current_x, current_y, new_x, new_y);

            if (current_y == 4 + color && board[new_x, current_y] != 0 && board[new_x, current_y] >> 4 != color && (board[new_x, current_y] & 0b00001111) == 0b00000010)
                Output.Add(make_non_normal_move(board, current_x, current_y, new_x, new_y, 0));
        }

        new_x = current_x - 1;
        if (new_x >= 1)
        {
            if (board[new_x, new_y] != 0 && board[new_x, new_y] >> 4 != color)
                Output = make_normal_pawn_moves(Output, board, current_x, current_y, new_x, new_y);

            if (current_y == 4 + color && board[new_x, current_y] != 0 && board[new_x, current_y] >> 4 != color && (board[new_x, current_y] & 0b00001111) == 0b00000010)
                Output.Add(make_non_normal_move(board, current_x, current_y, new_x, new_y, 0));
        }

        return Output;
    }
    public void KnightCheck(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        if (PositionX + 2 <= 8)
        {
            if (PositionY - 1 >= 1 && InputBoard[PositionX + 2, PositionY - 1] != 0 && (InputBoard[PositionX + 2, PositionY - 1]) >> 4 != color)
                check(InputBoard, PositionX + 2, PositionY - 1);
            if (PositionY + 1 <= 8 && InputBoard[PositionX + 2, PositionY + 1] != 0 && (InputBoard[PositionX + 2, PositionY + 1]) >> 4 != color)
                check(InputBoard, PositionX + 2, PositionY + 1);
        }
        if (PositionX - 2 >= 1)
        {
            if (PositionY - 1 >= 1 && InputBoard[PositionX - 2, PositionY - 1] != 0 && (InputBoard[PositionX - 2, PositionY - 1]) >> 4 != color)
                check(InputBoard, PositionX - 2, PositionY - 1);
            if (PositionY + 1 <= 8 && InputBoard[PositionX - 2, PositionY + 1] != 0 && (InputBoard[PositionX - 2, PositionY + 1]) >> 4 != color)
                check(InputBoard, PositionX - 2, PositionY + 1);
        }
        if (PositionX + 1 <= 8)
        {
            if (PositionY - 2 >= 1 && InputBoard[PositionX + 1, PositionY - 2] != 0 && (InputBoard[PositionX + 1, PositionY - 2]) >> 4 != color)
                check(InputBoard, PositionX + 1, PositionY - 2);
            if (PositionY + 2 <= 8 && InputBoard[PositionX + 1, PositionY + 2] != 0 && (InputBoard[PositionX + 1, PositionY + 2]) >> 4 != color)
                check(InputBoard, PositionX + 1, PositionY + 2);
        }
        if (PositionX - 1 >= 1)
        {
            if (PositionY - 2 >= 1 && InputBoard[PositionX - 1, PositionY - 2] != 0 && (InputBoard[PositionX - 1, PositionY - 2]) >> 4 != color)
                check(InputBoard, PositionX - 1, PositionY - 2);
            if (PositionY + 2 <= 8 && InputBoard[PositionX - 1, PositionY + 2] != 0 && (InputBoard[PositionX - 1, PositionY + 2]) >> 4 != color)
                check(InputBoard, PositionX - 1, PositionY + 2);
        }
    }
    public void BishopCheck(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        for (int i = 1; i + PositionX <= 8 && i + PositionY <= 8; i++)
        {
            if (InputBoard[PositionX + i, PositionY + i] != 0)
            {
                if (InputBoard[PositionX + i, PositionY + i] >> 4 != color)
                {
                    check(InputBoard, PositionX + i, PositionY + i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; PositionX - i >= 1 && PositionY - i >= 1; i++)
        {
            if (InputBoard[PositionX - i, PositionY - i] != 0)
            {
                if (InputBoard[PositionX - i, PositionY - i] >> 4 != color)
                {
                    check(InputBoard, PositionX - i, PositionY - i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; PositionX - i >= 1 && i + PositionY <= 8; i++)
        {
            if (InputBoard[PositionX - i, PositionY + i] != 0)
            {
                if (InputBoard[PositionX - i, PositionY + i] >> 4 != color)
                {
                    check(InputBoard, PositionX - i, PositionY + i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; i + PositionX <= 8 && PositionY - i >= 1; i++)
        {
            if (InputBoard[PositionX + i, PositionY - i] != 0)
            {
                if (InputBoard[PositionX + i, PositionY - i] >> 4 != color)
                {
                    check(InputBoard, PositionX + i, PositionY - i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }
    }
    public void RookCheck(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        for (int i = PositionX + 1; i <= 8; i++)
        {
            if (InputBoard[i, PositionY] != 0)
            {
                if (InputBoard[i, PositionY] >> 4 != color)
                {
                    check(InputBoard, i, PositionY);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = PositionX - 1; i >= 1; i--)
        {
            if (InputBoard[i, PositionY] != 0)
            {
                if (InputBoard[i, PositionY] >> 4 != color)
                {
                    check(InputBoard, i, PositionY);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = PositionY + 1; i <= 8; i++)
        {
            if (InputBoard[PositionX, i] != 0)
            {
                if (InputBoard[PositionX, i] >> 4 != color)
                {
                    check(InputBoard, PositionX, i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = PositionY - 1; i >= 1; i--)
        {
            if (InputBoard[PositionX, i] != 0)
            {
                if (InputBoard[PositionX, i] >> 4 != color)
                {
                    check(InputBoard, PositionX, i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }
    }
    public void QueenCheck(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        for (int i = PositionX + 1; i <= 8; i++)
        {
            if (InputBoard[i, PositionY] != 0)
            {
                if (InputBoard[i, PositionY] >> 4 != color)
                {
                    check(InputBoard, i, PositionY);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = PositionX - 1; i >= 1; i--)
        {
            if (InputBoard[i, PositionY] != 0)
            {
                if (InputBoard[i, PositionY] >> 4 != color)
                {
                    check(InputBoard, i, PositionY);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = PositionY + 1; i <= 8; i++)
        {
            if (InputBoard[PositionX, i] != 0)
            {
                if (InputBoard[PositionX, i] >> 4 != color)
                {
                    check(InputBoard, PositionX, i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = PositionY - 1; i >= 1; i--)
        {
            if (InputBoard[PositionX, i] != 0)
            {
                if (InputBoard[PositionX, i] >> 4 != color)
                {
                    check(InputBoard, PositionX, i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; i + PositionX <= 8 && i + PositionY <= 8; i++)
        {
            if (InputBoard[PositionX + i, PositionY + i] != 0)
            {
                if (InputBoard[PositionX + i, PositionY + i] >> 4 != color)
                {
                    check(InputBoard, PositionX + i, PositionY + i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; PositionX - i >= 1 && PositionY - i >= 1; i++)
        {
            if (InputBoard[PositionX - i, PositionY - i] != 0)
            {
                if (InputBoard[PositionX - i, PositionY - i] >> 4 != color)
                {
                    check(InputBoard, PositionX - i, PositionY - i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; PositionX - i >= 1 && i + PositionY <= 8; i++)
        {
            if (InputBoard[PositionX - i, PositionY + i] != 0)
            {
                if (InputBoard[PositionX - i, PositionY + i] >> 4 != color)
                {
                    check(InputBoard, PositionX - i, PositionY + i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 1; i + PositionX <= 8 && PositionY - i >= 1; i++)
        {
            if (InputBoard[PositionX + i, PositionY - i] != 0)
            {
                if (InputBoard[PositionX + i, PositionY - i] >> 4 != color)
                {
                    check(InputBoard, PositionX + i, PositionY - i);
                    break;
                }
                else
                {
                    break;
                }
            }
        }
    }
    public void KingCheck(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        // normal Mooves
        if (PositionX - 1 >= 1)
        {
            if (InputBoard[PositionX - 1, PositionY] != 0 && InputBoard[PositionX - 1, PositionY] >> 4 != color)
                check(InputBoard, PositionX - 1, PositionY);
            if (PositionY + 1 <= 8 && InputBoard[PositionX - 1, PositionY + 1] != 0 && InputBoard[PositionX - 1, PositionY + 1] >> 4 != color)
                check(InputBoard, PositionX - 1, PositionY + 1);
            if (PositionY - 1 >= 1 && InputBoard[PositionX - 1, PositionY - 1] != 0 && InputBoard[PositionX - 1, PositionY - 1] >> 4 != color)
                check(InputBoard, PositionX - 1, PositionY - 1);
        }
        if (PositionX + 1 <= 8)
        {
            if (InputBoard[PositionX + 1, PositionY] != 0 && InputBoard[PositionX + 1, PositionY] >> 4 != color)
                check(InputBoard, PositionX + 1, PositionY);
            if (PositionY + 1 <= 8 && InputBoard[PositionX + 1, PositionY + 1] != 0 && InputBoard[PositionX + 1, PositionY + 1] >> 4 != color)
                check(InputBoard, PositionX + 1, PositionY + 1);
            if (PositionY - 1 >= 1 && InputBoard[PositionX + 1, PositionY - 1] != 0 && InputBoard[PositionX + 1, PositionY - 1] >> 4 != color)
                check(InputBoard, PositionX + 1, PositionY - 1);
        }

        if (PositionY + 1 <= 8 && InputBoard[PositionX, PositionY + 1] != 0 && InputBoard[PositionX, PositionY + 1] >> 4 != color)
            check(InputBoard, PositionX, PositionY + 1);
        if (PositionY - 1 >= 1 && InputBoard[PositionX, PositionY - 1] != 0 && InputBoard[PositionX, PositionY - 1] >> 4 != color)
            check(InputBoard, PositionX, PositionY - 1);
    }
    public void PawnCheck(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        int colorminus = 2 * color - 1;

        if (PositionX + 1 <= 8 && InputBoard[PositionX + 1, PositionY + colorminus] != 0 && InputBoard[PositionX + 1, PositionY + colorminus] >> 4 != color)
            check(InputBoard, PositionX + 1, PositionY + colorminus);
        if (PositionX - 1 >= 1 && InputBoard[PositionX - 1, PositionY + colorminus] != 0 && InputBoard[PositionX - 1, PositionY + colorminus] >> 4 != color)
            check(InputBoard, PositionX - 1, PositionY + colorminus);
    }
    public bool IsPositionIllegal(byte[,] InputBoard, byte color, int KingX, int KingY)
    {
        bool Output = false;

        return Output;
    }
}
