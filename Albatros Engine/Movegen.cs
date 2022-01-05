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
    public int[] UnmakeMove;
    bool WrongPosition = false;
    int[] kingpositions = new int[4];
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
    public int Mate(byte[,] InputBoard, byte Color)
    {
        //Look for blocking of Position
        List<int[]> Moves = ReturnPossibleMoves(InputBoard, Color);
        if (Moves == null)
            return -2;
        byte[,] MoveUndo = new byte[9, 9];
        byte NewColor = 0;
        if (Color == 0)
            NewColor = 1;
        foreach (int[] Move in Moves)
        {
            if (Move.Length != 5 || !CastlingCheck(InputBoard, Move))
            {
                Array.Copy(InputBoard, MoveUndo, MoveUndo.Length);
                InputBoard = PlayMove(InputBoard, Color, Move);
                if (!CompleteCheck(InputBoard, NewColor))
                {
                    Array.Copy(MoveUndo, InputBoard, InputBoard.Length);
                    return 2;
                }
                else
                    Array.Copy(MoveUndo, InputBoard, InputBoard.Length);
            }
        }
        //Mate or Stalemate
        //Case: Mate
        if (CompleteCheck(InputBoard, NewColor))
            return (2 * NewColor - 1);
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
    public byte[,] PlayMove(byte[,] InputBoard, byte color, int[] Moove)
    {
        for (int x = 1; x < 9; x++)
            for (int y = 4; y < 6; y++)
                if (InputBoard[x, y] != 0 && InputBoard[x, y] >> 4 == color && InputBoard[x, y] - (InputBoard[x, y] >> 4) * 0b10000 == 0b10)
                    InputBoard[x, y]++;
        int k = Moove[0];
        int j = Moove[1];
        int[] CurrentMoove;
        if (Moove.Length == 4)
            CurrentMoove = new int[2] { Moove[2], Moove[3] };
        else if (Moove.Length == 5)
            CurrentMoove = new int[3] { Moove[2], Moove[3], Moove[4] };
        else
            CurrentMoove = new int[0];
        switch (InputBoard[k, j] - (InputBoard[k, j] >> 4) * 0b10000)
        {
            //PawnStart
            case 0b00000001:
                Array.Copy(PawnStartExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //NormalPawn
            case 0b00000011:
                Array.Copy(PawnDirectExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //Knight
            case 0b00000100:
                Array.Copy(NormalExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //Bishop
            case 0b00000101:
                Array.Copy(NormalExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //King Can Castle
            case 0b00000110:
                Array.Copy(KingCastleExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //Normal King
            case 0b00000111:
                Array.Copy(NormalExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //Queen
            case 0b00001000:
                Array.Copy(NormalExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //RookCanCastle
            case 0b00001001:
                Array.Copy(StartExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
            //Normal Rook
            case 0b00001010:
                Array.Copy(NormalExecuteMooves(InputBoard, CurrentMoove, k, j), InputBoard, InputBoard.Length);
                break;
        }
        return InputBoard;
    }
    public byte[,] PawnDirectExecuteMooves(byte[,] InputBoard, int[] Move, int X, int Y)
    {
        byte Copy = InputBoard[X, Y];

        //Queening
        if (Move.Length == 3)
        {
            switch (Move[2])
            {
                // EnPassent
                case 0:
                    UnmakeMove = new int[] { X, Y, Copy, Move[0], Move[1], 0, Move[0], Y, InputBoard[Move[0], Y] };
                    InputBoard[Move[0], Y] = 0;
                    break;
                case 1:
                    //Knight
                    Copy++;
                    UnmakeMove = new int[] { X, Y, Copy, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
                    break;
                case 2:
                    //Bishop
                    Copy += 2;
                    UnmakeMove = new int[] { X, Y, Copy, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
                    break;
                case 3:
                    //Queen
                    Copy += 5;
                    UnmakeMove = new int[] { X, Y, Copy, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
                    break;
                case 4:
                    //Rook
                    Copy += 7;
                    UnmakeMove = new int[] { X, Y, Copy, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
                    break;
            }
        }
        else
        {
            UnmakeMove = new int[] { X, Y, Copy, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
        }
        InputBoard[X, Y] = 0;
        InputBoard[Move[0], Move[1]] = Copy;

        return InputBoard;
    }
    public byte[,] KingCastleExecuteMooves(byte[,] InputBoard, int[] Move, int X, int Y)
    {
        byte Copy = (byte)(InputBoard[X, Y] + 1);

        if (Move.Length == 2)
        {
            UnmakeMove = new int[] { X, Y, Copy - 1, Move[0], Move[1], InputBoard[Move[0], Move[1]] };
            InputBoard[X, Y] = 0;
            InputBoard[Move[0], Move[1]] = Copy;
        }
        // if Castelling
        else
        {
            byte KingColor = (byte)(InputBoard[X, Y] >> 4);
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
    public bool CastlingCheck(byte[,] InputBoard, int[] Moove)
    {
        byte Copy = (byte)(InputBoard[Moove[0], Moove[1]] + 1);
        bool Output = false;
        byte[,] CurrentBoard = new byte[9, 9];

        Array.Copy(InputBoard, 0, CurrentBoard, 0, InputBoard.Length);

        if (Moove.Length == 5)
        {
            byte KingColor = (byte)(InputBoard[Moove[0], Moove[1]] >> 4);
            byte EnemyColor = 0;
            if (KingColor == 0)
                EnemyColor = 1;

            //Copy the King to two new Squares
            CurrentBoard[(Moove[0] + Moove[2]) / 2, Moove[1]] = Copy;
            CurrentBoard[Moove[2], Moove[3]] = Copy;

            //Check if any of the Kings are in Check
            if (CompleteCheck(CurrentBoard, EnemyColor))
                Output = true;
        }
        return Output;
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
    public List<int[]> ReturnPossibleMoves(byte[,] InputBoard, byte color)
    {
        List<int[]> Output = new List<int[]>();
        for (int i = 1; i < 9; i++)
            for (int j = 4; j < 6; j++)
                if (InputBoard[i, j] != 0 && InputBoard[i, j] >> 4 == color && InputBoard[i, j] - (InputBoard[i, j] >> 4) * 0b10000 == 0b10)
                    InputBoard[i, j]++;
        for (int i = 1; i < 9; i++)
        {
            for (int j = 1; j < 9; j++)
            {
                if (InputBoard[i, j] != 0 && InputBoard[i, j] >> 4 == color)
                {
                    switch (InputBoard[i, j] - (InputBoard[i, j] >> 4) * 0b10000)
                    {
                        case 0b00000001:
                            Output.AddRange(UpdatePieceOutput(PawnMoove(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00000011:
                            Output.AddRange(UpdatePawnOutput(PawnMoove(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00000100:
                            Output.AddRange(UpdatePieceOutput(KnightMoove(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00000101:
                            Output.AddRange(UpdatePieceOutput(BishopMoove(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00000110:
                            Output.AddRange(UpdatePieceOutput(KingMoove(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00000111:
                            Output.AddRange(UpdatePieceOutput(KingMoove(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00001000:
                            Output.AddRange(UpdatePieceOutput(QueenMoove(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00001001:
                            Output.AddRange(UpdatePieceOutput(RookMoove(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00001010:
                            Output.AddRange(UpdatePieceOutput(RookMoove(InputBoard, i, j, color), i, j));
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
            for (int j = 4; j < 6; j++)
                if (InputBoard[i, j] != 0 && InputBoard[i, j] >> 4 == color && InputBoard[i, j] - (InputBoard[i, j] >> 4) * 0b10000 == 0b10)
                    InputBoard[i, j]++;
        for (int i = 1; i < 9; i++)
        {
            for (int j = 1; j < 9; j++)
            {
                if (InputBoard[i, j] != 0 && InputBoard[i, j] >> 4 == color)
                {
                    switch (InputBoard[i, j] - (InputBoard[i, j] >> 4) * 0b10000)
                    {
                        case 0b00000001:
                            Output.AddRange(UpdatePieceOutput(PawnCaptures(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00000011:
                            Output.AddRange(UpdatePawnOutput(PawnCaptures(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00000100:
                            Output.AddRange(UpdatePieceOutput(KnightCaptures(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00000101:
                            Output.AddRange(UpdatePieceOutput(BishopCaptures(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00000110:
                            Output.AddRange(UpdatePieceOutput(KingCaptures(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00000111:
                            Output.AddRange(UpdatePieceOutput(KingCaptures(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00001000:
                            Output.AddRange(UpdatePieceOutput(QueenCaptures(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00001001:
                            Output.AddRange(UpdatePieceOutput(RookCaptures(InputBoard, i, j, color), i, j));
                            break;
                        case 0b00001010:
                            Output.AddRange(UpdatePieceOutput(RookCaptures(InputBoard, i, j, color), i, j));
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
    public List<int[]> UpdatePieceOutput(List<int[]> InputList, int X, int Y)
    {
        List<int[]> OutputList = new List<int[]>();
        foreach (int[] Array in InputList)
        {
            if (Array.Length == 2)
                OutputList.Add(new int[4] { X, Y, Array[0], Array[1] });
            else
                OutputList.Add(new int[5] { X, Y, Array[0], Array[1], 0 });
        }
        return OutputList;
    }
    public List<int[]> UpdatePawnOutput(List<int[]> InputList, int X, int Y)
    {
        List<int[]> OutputList = new List<int[]>();
        foreach (int[] Array in InputList)
        {
            //if Promoting Pawn
            if (Array[1] == 8 || Array[1] == 1)
            {
                //Knight
                OutputList.Add(new int[5] { X, Y, Array[0], Array[1], 1 });
                //Bishop
                OutputList.Add(new int[5] { X, Y, Array[0], Array[1], 2 });
                //Queen 
                OutputList.Add(new int[5] { X, Y, Array[0], Array[1], 3 });
                //Rook
                OutputList.Add(new int[5] { X, Y, Array[0], Array[1], 4 });
            }
            else if (Array.Length == 3)
            {
                OutputList.Add(new int[5] { X, Y, Array[0], Array[1], 0 });
            }
            else
            {
                OutputList.Add(new int[4] { X, Y, Array[0], Array[1] });
            }
        }
        return OutputList;
    }
    public void check(byte[,] InputBoard, int X, int Y)
    {
        if ((InputBoard[X, Y] - (InputBoard[X, Y] >> 4) * 0b10000) >> 1 == 0b11)
        {
            WrongPosition = true;
        }
    }
    public bool CompleteCheck(byte[,] InputBoard, byte color)
    {
        for (int i = 1; i < 9; i++)
            for (int j = 4; j < 6; j++)
                if (InputBoard[i, j] != 0 && InputBoard[i, j] >> 4 == color && InputBoard[i, j] - (InputBoard[i, j] >> 4) * 0b10000 == 0b10)
                    InputBoard[i, j]++;
        for (int i = 1; i < 9; i++)
        {
            for (int j = 1; j < 9; j++)
            {
                if (InputBoard[i, j] != 0 && InputBoard[i, j] >> 4 == color)
                {
                    switch (InputBoard[i, j] - (InputBoard[i, j] >> 4) * 0b10000)
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
    public List<int[]> KnightMoove(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        List<int[]> Output = new List<int[]>();

        if (PositionX + 2 <= 8)
        {
            if (PositionY - 1 >= 1 && InputBoard[PositionX + 2, PositionY - 1] == 0 || PositionY - 1 >= 1 && (InputBoard[PositionX + 2, PositionY - 1]) >> 4 != color)
            {
                check(InputBoard, PositionX + 2, PositionY - 1);
                Output.Add(new int[2] { PositionX + 2, PositionY - 1 });
            }
            if (PositionY + 1 <= 8 && InputBoard[PositionX + 2, PositionY + 1] == 0 || PositionY + 1 <= 8 && (InputBoard[PositionX + 2, PositionY + 1]) >> 4 != color)
            {
                check(InputBoard, PositionX + 2, PositionY + 1);
                Output.Add(new int[2] { PositionX + 2, PositionY + 1 });
            }

        }
        if (PositionX - 2 >= 1)
        {
            if (PositionY - 1 >= 1 && InputBoard[PositionX - 2, PositionY - 1] == 0 || PositionY - 1 >= 1 && (InputBoard[PositionX - 2, PositionY - 1]) >> 4 != color)
            {
                check(InputBoard, PositionX - 2, PositionY - 1);
                Output.Add(new int[2] { PositionX - 2, PositionY - 1 });
            }
            if (PositionY + 1 <= 8 && InputBoard[PositionX - 2, PositionY + 1] == 0 || PositionY + 1 <= 8 && (InputBoard[PositionX - 2, PositionY + 1]) >> 4 != color)
            {
                check(InputBoard, PositionX - 2, PositionY + 1);
                Output.Add(new int[2] { PositionX - 2, PositionY + 1 });
            }

        }
        if (PositionX + 1 <= 8)
        {
            if (PositionY - 2 >= 1 && InputBoard[PositionX + 1, PositionY - 2] == 0 || PositionY - 2 >= 1 && (InputBoard[PositionX + 1, PositionY - 2]) >> 4 != color)
            {
                check(InputBoard, PositionX + 1, PositionY - 2);
                Output.Add(new int[2] { PositionX + 1, PositionY - 2 });
            }


            if (PositionY + 2 <= 8 && InputBoard[PositionX + 1, PositionY + 2] == 0 || PositionY + 2 <= 8 && (InputBoard[PositionX + 1, PositionY + 2]) >> 4 != color)
            {
                check(InputBoard, PositionX + 1, PositionY + 2);
                Output.Add(new int[2] { PositionX + 1, PositionY + 2 });
            }
        }
        if (PositionX - 1 >= 1)
        {
            if (PositionY - 2 >= 1 && InputBoard[PositionX - 1, PositionY - 2] == 0  || PositionY - 2 >= 1 && (InputBoard[PositionX - 1, PositionY - 2]) >> 4 != color)
            {
                check(InputBoard, PositionX - 1, PositionY - 2);
                Output.Add(new int[2] { PositionX - 1, PositionY - 2 });
            }
            if (PositionY + 2 <= 8 && InputBoard[PositionX - 1, PositionY + 2] == 0 || PositionY + 2 <= 8 && (InputBoard[PositionX - 1, PositionY + 2]) >> 4 != color)
            {
                check(InputBoard, PositionX - 1, PositionY + 2);
                Output.Add(new int[2] { PositionX - 1, PositionY + 2 });
            }
        }
        return Output;
    }
    public List<int[]> BishopMoove(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        List<int[]> Output = new List<int[]>();

        for (int i = 1; i + PositionX <= 8 && i + PositionY <= 8; i++)
        {
            if (InputBoard[PositionX + i, PositionY + i] == 0)
                Output.Add(new int[2] { PositionX + i, PositionY + i });

            else
            {
                if (InputBoard[PositionX + i, PositionY + i] >> 4 != color)
                {
                    check(InputBoard, PositionX + i, PositionY + i);
                    Output.Add(new int[2] { PositionX + i, PositionY + i });
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
            if (InputBoard[PositionX - i, PositionY - i] == 0)
                Output.Add(new int[2] { PositionX - i, PositionY - i });

            else
            {
                if (InputBoard[PositionX - i, PositionY - i] >> 4 != color)
                {
                    check(InputBoard, PositionX - i, PositionY - i);
                    Output.Add(new int[2] { PositionX - i, PositionY - i });
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
            if (InputBoard[PositionX - i, PositionY + i] == 0)
                Output.Add(new int[2] { PositionX - i, PositionY + i });

            else
            {
                if (InputBoard[PositionX - i, PositionY + i] >> 4 != color)
                {
                    check(InputBoard, PositionX - i, PositionY + i);
                    Output.Add(new int[2] { PositionX - i, PositionY + i });
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
            if (InputBoard[PositionX + i, PositionY - i] == 0 )
                Output.Add(new int[2] { PositionX + i, PositionY - i });

            else
            {
                if (InputBoard[PositionX + i, PositionY - i] >> 4 != color)
                {
                    check(InputBoard, PositionX + i, PositionY - i);
                    Output.Add(new int[2] { PositionX + i, PositionY - i });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        return Output;
    }
    public List<int[]> RookMoove(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        List<int[]> Output = new List<int[]>();

        for (int i = PositionX + 1; i <= 8; i++)
        {
            if (InputBoard[i, PositionY] == 0)
                Output.Add(new int[2] { i, PositionY });

            else
            {
                if (InputBoard[i, PositionY] >> 4 != color)
                {
                    check(InputBoard, i, PositionY);
                    Output.Add(new int[2] { i, PositionY });
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
            if (InputBoard[i, PositionY] == 0)
                Output.Add(new int[2] { i, PositionY });

            else
            {
                if (InputBoard[i, PositionY] >> 4 != color)
                {
                    check(InputBoard, i, PositionY);
                    Output.Add(new int[2] { i, PositionY });
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
            if (InputBoard[PositionX, i] == 0)
                Output.Add(new int[2] { PositionX, i });

            else
            {
                if (InputBoard[PositionX, i] >> 4 != color)
                {
                    check(InputBoard, PositionX, i);
                    Output.Add(new int[2] { PositionX, i });
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
            if (InputBoard[PositionX, i] == 0 )
                Output.Add(new int[2] { PositionX, i });

            else
            {
                if (InputBoard[PositionX, i] >> 4 != color)
                {
                    check(InputBoard, PositionX, i);
                    Output.Add(new int[2] { PositionX, i });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        return Output;

    }
    public List<int[]> QueenMoove(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        List<int[]> Output = new List<int[]>();

        for (int i = 1; i + PositionX <= 8 && i + PositionY <= 8; i++)
        {
            if (InputBoard[PositionX + i, PositionY + i] == 0)
                Output.Add(new int[2] { PositionX + i, PositionY + i });

            else
            {
                if (InputBoard[PositionX + i, PositionY + i] >> 4 != color)
                {
                    check(InputBoard, PositionX + i, PositionY + i);
                    Output.Add(new int[2] { PositionX + i, PositionY + i });
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
            if (InputBoard[PositionX - i, PositionY - i] == 0)
                Output.Add(new int[2] { PositionX - i, PositionY - i });

            else
            {
                if (InputBoard[PositionX - i, PositionY - i] >> 4 != color)
                {
                    check(InputBoard, PositionX - i, PositionY - i);
                    Output.Add(new int[2] { PositionX - i, PositionY - i });
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
            if (InputBoard[PositionX - i, PositionY + i] == 0)
                Output.Add(new int[2] { PositionX - i, PositionY + i });

            else
            {
                if (InputBoard[PositionX - i, PositionY + i] >> 4 != color)
                {
                    check(InputBoard, PositionX - i, PositionY + i);
                    Output.Add(new int[2] { PositionX - i, PositionY + i });
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
            if (InputBoard[PositionX + i, PositionY - i] == 0)
                Output.Add(new int[2] { PositionX + i, PositionY - i });

            else
            {
                if (InputBoard[PositionX + i, PositionY - i] >> 4 != color)
                {
                    check(InputBoard, PositionX + i, PositionY - i);
                    Output.Add(new int[2] { PositionX + i, PositionY - i });
                    break;
                }
                else
                {
                    break;
                }
            }
        }
        for (int i = PositionX + 1; i <= 8; i++)
        {
            if (InputBoard[i, PositionY] == 0)
                Output.Add(new int[2] { i, PositionY });

            else
            {
                if (InputBoard[i, PositionY] >> 4 != color)
                {
                    check(InputBoard, i, PositionY);
                    Output.Add(new int[2] { i, PositionY });
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
            if (InputBoard[i, PositionY] == 0)
                Output.Add(new int[2] { i, PositionY });

            else
            {
                if (InputBoard[i, PositionY] >> 4 != color)
                {
                    check(InputBoard, i, PositionY);
                    Output.Add(new int[2] { i, PositionY });
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
            if (InputBoard[PositionX, i] == 0)
                Output.Add(new int[2] { PositionX, i });

            else
            {
                if (InputBoard[PositionX, i] >> 4 != color)
                {
                    check(InputBoard, PositionX, i);
                    Output.Add(new int[2] { PositionX, i });
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
            if (InputBoard[PositionX, i] == 0)
                Output.Add(new int[2] { PositionX, i });

            else
            {
                if (InputBoard[PositionX, i] >> 4 != color)
                {
                    check(InputBoard, PositionX, i);
                    Output.Add(new int[2] { PositionX, i });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        return Output;
    }
    public List<int[]> KingMoove(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        List<int[]> Output = new List<int[]>();

        //Casteling
        if (InputBoard[PositionX, PositionY] - color * 0b10000 == 0b00000110 && InputBoard[PositionX + 1, PositionY] == 0 && InputBoard[PositionX + 2, PositionY] == 0 && InputBoard[PositionX + 3, PositionY] != 0 && InputBoard[PositionX + 3, PositionY] - color * 0b10000 == 0b00001001)
        {
            Output.Add(new int[3] { PositionX + 2, PositionY, 0 });
        }
        if (InputBoard[PositionX, PositionY] - color * 0b10000 == 0b00000110 && InputBoard[PositionX - 1, PositionY] == 0 && InputBoard[PositionX - 2, PositionY] == 0 && InputBoard[PositionX - 3, PositionY] == 0 && InputBoard[PositionX - 4, PositionY] != 0 && InputBoard[PositionX - 4, PositionY] - color * 0b10000 == 0b00001001 )
        {
            Output.Add(new int[3] { PositionX - 2, PositionY, 0 });
        }

        // normal Mooves
        if (PositionX - 1 >= 1)
        {
            if (InputBoard[PositionX - 1, PositionY] == 0|| InputBoard[PositionX - 1, PositionY] >> 4 != color)
            {
                check(InputBoard, PositionX - 1, PositionY);
                Output.Add(new int[2] { PositionX - 1, PositionY });
            }
            if (PositionY + 1 <= 8 && InputBoard[PositionX - 1, PositionY + 1] == 0|| PositionY + 1 <= 8 && InputBoard[PositionX - 1, PositionY + 1] >> 4 != color)
            {
                check(InputBoard, PositionX - 1, PositionY + 1);
                Output.Add(new int[2] { PositionX - 1, PositionY + 1 });
            }
            if (PositionY - 1 >= 1 && InputBoard[PositionX - 1, PositionY - 1] == 0 || PositionY - 1 >= 1 && InputBoard[PositionX - 1, PositionY - 1] >> 4 != color)
            {
                check(InputBoard, PositionX - 1, PositionY - 1);
                Output.Add(new int[2] { PositionX - 1, PositionY - 1 });
            }
        }
        if (PositionX + 1 <= 8)
        {
            if (InputBoard[PositionX + 1, PositionY] == 0 || InputBoard[PositionX + 1, PositionY] >> 4 != color)
            {
                check(InputBoard, PositionX + 1, PositionY);
                Output.Add(new int[2] { PositionX + 1, PositionY });
            }
            if (PositionY + 1 <= 8 && InputBoard[PositionX + 1, PositionY + 1] == 0 || PositionY + 1 <= 8 && InputBoard[PositionX + 1, PositionY + 1] >> 4 != color)
            {
                check(InputBoard, PositionX + 1, PositionY + 1);
                Output.Add(new int[2] { PositionX + 1, PositionY + 1 });
            }
            if (PositionY - 1 >= 1 && InputBoard[PositionX + 1, PositionY - 1] == 0 || PositionY - 1 >= 1 && InputBoard[PositionX + 1, PositionY - 1] >> 4 != color)
            {
                check(InputBoard, PositionX + 1, PositionY - 1);
                Output.Add(new int[2] { PositionX + 1, PositionY - 1 });
            }
        }

        if (PositionY + 1 <= 8 && InputBoard[PositionX, PositionY + 1] == 0 || PositionY + 1 <= 8 && InputBoard[PositionX, PositionY + 1] >> 4 != color)
        {
            check(InputBoard, PositionX, PositionY + 1);
            Output.Add(new int[2] { PositionX, PositionY + 1 });
        }
        if (PositionY - 1 >= 1 && InputBoard[PositionX, PositionY - 1] == 0|| PositionY - 1 >= 1 && InputBoard[PositionX, PositionY - 1] >> 4 != color)
        {
            check(InputBoard, PositionX, PositionY - 1);
            Output.Add(new int[2] { PositionX, PositionY - 1 });
        }

        return Output;
    }
    public List<int[]> PawnMoove(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        List<int[]> Output = new List<int[]>();
        int colorminus = 1;

        if (color == 0)
            colorminus = -1;

        if (InputBoard[PositionX, PositionY] - color * 0b10000 == 0b1 && InputBoard[PositionX, PositionY + colorminus] == 0 && InputBoard[PositionX, PositionY + 2 * colorminus] == 0)
            Output.Add(new int[2] { PositionX, PositionY + 2 * colorminus });


        if (PositionY + colorminus >= 1 && PositionY + colorminus <= 8)
        {
            if (InputBoard[PositionX, PositionY + colorminus] == 0)
                Output.Add(new int[2] { PositionX, PositionY + 1 * colorminus });

            if (PositionX + 1 <= 8 && InputBoard[PositionX + 1, PositionY + colorminus] != 0 && InputBoard[PositionX + 1, PositionY + colorminus] >> 4 != color)
            {
                check(InputBoard, PositionX + 1, PositionY + 1 * colorminus);
                Output.Add(new int[2] { PositionX + 1, PositionY + 1 * colorminus });
            }
            if (PositionX - 1 >= 1 && InputBoard[PositionX - 1, PositionY + colorminus] != 0 && InputBoard[PositionX - 1, PositionY + colorminus] >> 4 != color)
            {
                check(InputBoard, PositionX - 1, PositionY + 1 * colorminus);
                Output.Add(new int[2] { PositionX - 1, PositionY + 1 * colorminus });
            }
        }
        //en passent
        if (PositionX + 1 <= 8 && PositionY == 4 + color && InputBoard[PositionX + 1, PositionY] != 0 && InputBoard[PositionX + 1, PositionY] >> 4 != color && InputBoard[PositionX + 1, PositionY] - (InputBoard[PositionX + 1, PositionY] >> 4) * 0b10000 == 0b00000010)
            Output.Add(new int[3] { PositionX + 1, PositionY + 1 * colorminus, 0 });

        if (PositionX - 1 >= 1 && PositionY == 4 + color && InputBoard[PositionX - 1, PositionY] != 0 && InputBoard[PositionX - 1, PositionY] >> 4 != color && InputBoard[PositionX - 1, PositionY] - (InputBoard[PositionX - 1, PositionY] >> 4) * 0b10000 == 0b00000010)
            Output.Add(new int[3] { PositionX - 1, PositionY + 1 * colorminus, 0 });
        return Output;
    }
    public List<int[]> KnightCaptures(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        List<int[]> Output = new List<int[]>();

        if (PositionX + 2 <= 8)
        {
            if (PositionY - 1 >= 1 && InputBoard[PositionX + 2, PositionY - 1] != 0 && (InputBoard[PositionX + 2, PositionY - 1]) >> 4 != color)
            {
                check(InputBoard, PositionX + 2, PositionY - 1);
                Output.Add(new int[2] { PositionX + 2, PositionY - 1 });
            }
            if (PositionY + 1 <= 8 && InputBoard[PositionX + 2, PositionY + 1] != 0 && (InputBoard[PositionX + 2, PositionY + 1]) >> 4 != color) 
            {
                check(InputBoard, PositionX + 2, PositionY + 1);
                Output.Add(new int[2] { PositionX + 2, PositionY + 1 });
            }

        }
        if (PositionX - 2 >= 1)
        {
            if (PositionY - 1 >= 1 && InputBoard[PositionX - 2, PositionY - 1] != 0 && (InputBoard[PositionX - 2, PositionY - 1]) >> 4 != color) 
            {
                check(InputBoard, PositionX - 2, PositionY - 1);
                Output.Add(new int[2] { PositionX - 2, PositionY - 1 });
            }
            if (PositionY + 1 <= 8 && InputBoard[PositionX - 2, PositionY + 1] != 0 && (InputBoard[PositionX - 2, PositionY + 1]) >> 4 != color) 
            {
                check(InputBoard, PositionX - 2, PositionY + 1);
                Output.Add(new int[2] { PositionX - 2, PositionY + 1 });
            }

        }
        if (PositionX + 1 <= 8)
        {
            if (PositionY - 2 >= 1 && InputBoard[PositionX + 1, PositionY - 2] != 0 && (InputBoard[PositionX + 1, PositionY - 2]) >> 4 != color) 
            {
                check(InputBoard, PositionX + 1, PositionY - 2);
                Output.Add(new int[2] { PositionX + 1, PositionY - 2 });
            }


            if (PositionY + 2 <= 8 && InputBoard[PositionX + 1, PositionY + 2] != 0 && (InputBoard[PositionX + 1, PositionY + 2]) >> 4 != color)
            {
                check(InputBoard, PositionX + 1, PositionY + 2);
                Output.Add(new int[2] { PositionX + 1, PositionY + 2 });
            }
        }
        if (PositionX - 1 >= 1)
        {
            if (PositionY - 2 >= 1 && InputBoard[PositionX - 1, PositionY - 2] != 0 && (InputBoard[PositionX - 1, PositionY - 2]) >> 4 != color) 
            {
                check(InputBoard, PositionX - 1, PositionY - 2);
                Output.Add(new int[2] { PositionX - 1, PositionY - 2 });
            }
            if (PositionY + 2 <= 8 && InputBoard[PositionX - 1, PositionY + 2] != 0 && (InputBoard[PositionX - 1, PositionY + 2]) >> 4 != color) 
            {
                check(InputBoard, PositionX - 1, PositionY + 2);
                Output.Add(new int[2] { PositionX - 1, PositionY + 2 });
            }
        }
        return Output;
    }
    public List<int[]> BishopCaptures(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        List<int[]> Output = new List<int[]>();

        for (int i = 1; i + PositionX <= 8 && i + PositionY <= 8; i++)
        {
            if (InputBoard[PositionX + i, PositionY + i] != 0)
            {
                if (InputBoard[PositionX + i, PositionY + i] >> 4 != color)
                {
                    check(InputBoard, PositionX + i, PositionY + i);
                    Output.Add(new int[2] { PositionX + i, PositionY + i });
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
                    Output.Add(new int[2] { PositionX - i, PositionY - i });
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
                    Output.Add(new int[2] { PositionX - i, PositionY + i });
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
                    Output.Add(new int[2] { PositionX + i, PositionY - i });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        return Output;
    }
    public List<int[]> RookCaptures(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        List<int[]> Output = new List<int[]>();

        for (int i = PositionX + 1; i <= 8; i++)
        {
            if (InputBoard[i, PositionY] != 0)
            {
                if (InputBoard[i, PositionY] >> 4 != color)
                {
                    check(InputBoard, i, PositionY);
                    Output.Add(new int[2] { i, PositionY });
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
                    Output.Add(new int[2] { i, PositionY });
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
                    Output.Add(new int[2] { PositionX, i });
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
                    Output.Add(new int[2] { PositionX, i });
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        return Output;

    }
    public List<int[]> QueenCaptures(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        List<int[]> Output = new List<int[]>();
        for (int i = PositionX + 1; i <= 8; i++)
        {
            if (InputBoard[i, PositionY] != 0)
            {
                if (InputBoard[i, PositionY] >> 4 != color)
                {
                    check(InputBoard, i, PositionY);
                    Output.Add(new int[2] { i, PositionY });
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
                    Output.Add(new int[2] { i, PositionY });
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
                    Output.Add(new int[2] { PositionX, i });
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
                    Output.Add(new int[2] { PositionX, i });
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
                    Output.Add(new int[2] { PositionX + i, PositionY + i });
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
                    Output.Add(new int[2] { PositionX - i, PositionY - i });
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
                    Output.Add(new int[2] { PositionX - i, PositionY + i });
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
                    Output.Add(new int[2] { PositionX + i, PositionY - i });
                    break;
                }
                else
                {
                    break;
                }
            }
        }
        return Output;
    }
    public List<int[]> KingCaptures(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        List<int[]> Output = new List<int[]>();
        // normal Mooves
        if (PositionX - 1 >= 1)
        {
            if (InputBoard[PositionX - 1, PositionY] != 0 && InputBoard[PositionX - 1, PositionY] >> 4 != color)
            {
                check(InputBoard, PositionX - 1, PositionY);
                Output.Add(new int[2] { PositionX - 1, PositionY });
            }
            if (PositionY + 1 <= 8 && InputBoard[PositionX - 1, PositionY + 1] != 0 && InputBoard[PositionX - 1, PositionY + 1] >> 4 != color)
            {
                check(InputBoard, PositionX - 1, PositionY + 1);
                Output.Add(new int[2] { PositionX - 1, PositionY + 1 });
            }
            if (PositionY - 1 >= 1 && InputBoard[PositionX - 1, PositionY - 1] != 0&& InputBoard[PositionX - 1, PositionY - 1] >> 4 != color)
            {
                check(InputBoard, PositionX - 1, PositionY - 1);
                Output.Add(new int[2] { PositionX - 1, PositionY - 1 });
            }
        }
        if (PositionX + 1 <= 8)
        {
            if (InputBoard[PositionX + 1, PositionY] != 0 && InputBoard[PositionX + 1, PositionY] >> 4 != color)
            {
                check(InputBoard, PositionX + 1, PositionY);
                Output.Add(new int[2] { PositionX + 1, PositionY });
            }
            if (PositionY + 1 <= 8 && InputBoard[PositionX + 1, PositionY + 1] != 0  && InputBoard[PositionX + 1, PositionY + 1] >> 4 != color)
            {
                check(InputBoard, PositionX + 1, PositionY + 1);
                Output.Add(new int[2] { PositionX + 1, PositionY + 1 });
            }
            if (PositionY - 1 >= 1 && InputBoard[PositionX + 1, PositionY - 1] != 0  && InputBoard[PositionX + 1, PositionY - 1] >> 4 != color)
            {
                check(InputBoard, PositionX + 1, PositionY - 1);
                Output.Add(new int[2] { PositionX + 1, PositionY - 1 });
            }
        }

        if (PositionY + 1 <= 8 && InputBoard[PositionX, PositionY + 1] != 0 && InputBoard[PositionX, PositionY + 1] >> 4 != color)
        {
            check(InputBoard, PositionX, PositionY + 1);
            Output.Add(new int[2] { PositionX, PositionY + 1 });
        }
        if (PositionY - 1 >= 1 && InputBoard[PositionX, PositionY - 1] != 0 && InputBoard[PositionX, PositionY - 1] >> 4 != color)
        {
            check(InputBoard, PositionX, PositionY - 1);
            Output.Add(new int[2] { PositionX, PositionY - 1 });
        }

        return Output;
    }
    public List<int[]> PawnCaptures(byte[,] InputBoard, int PositionX, int PositionY, byte color)
    {
        List<int[]> Output = new List<int[]>();
        int colorminus = 1;

        if (color == 0)
            colorminus = -1;

        if (PositionY + colorminus >= 1 && PositionY + colorminus <= 8)
        {
            if (PositionX + 1 <= 8 && InputBoard[PositionX + 1, PositionY + colorminus] != 0 && InputBoard[PositionX + 1, PositionY + colorminus] >> 4 != color)
            {
                check(InputBoard, PositionX + 1, PositionY + 1 * colorminus);
                Output.Add(new int[2] { PositionX + 1, PositionY + 1 * colorminus });
            }
            if (PositionX - 1 >= 1 && InputBoard[PositionX - 1, PositionY + colorminus] != 0 && InputBoard[PositionX - 1, PositionY + colorminus] >> 4 != color)
            {
                check(InputBoard, PositionX - 1, PositionY + 1 * colorminus);
                Output.Add(new int[2] { PositionX - 1, PositionY + 1 * colorminus });
            }
        }
        //en passent
        if (PositionX + 1 <= 8 && PositionY == 4 + color && InputBoard[PositionX + 1, PositionY] != 0 && InputBoard[PositionX + 1, PositionY] >> 4 != color && InputBoard[PositionX + 1, PositionY] - (InputBoard[PositionX + 1, PositionY] >> 4) * 0b10000 == 0b00000010)
            Output.Add(new int[3] { PositionX + 1, PositionY + 1 * colorminus, 0 });

        if (PositionX - 1 >= 1 && PositionY == 4 + color && InputBoard[PositionX - 1, PositionY] != 0 && InputBoard[PositionX - 1, PositionY] >> 4 != color && InputBoard[PositionX - 1, PositionY] - (InputBoard[PositionX - 1, PositionY] >> 4) * 0b10000 == 0b00000010)
            Output.Add(new int[3] { PositionX - 1, PositionY + 1 * colorminus, 0 });
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
        int colorminus = 1;
        if (color == 0)
            colorminus = -1;
        if (PositionY + colorminus >= 1 && PositionY + colorminus <= 8)
        {
            if (PositionX + 1 <= 8 && InputBoard[PositionX + 1, PositionY + colorminus] != 0 && InputBoard[PositionX + 1, PositionY + colorminus] >> 4 != color)
            {
                check(InputBoard, PositionX + 1, PositionY + 1 * colorminus);
            }
            if (PositionX - 1 >= 1 && InputBoard[PositionX - 1, PositionY + colorminus] != 0 && InputBoard[PositionX - 1, PositionY + colorminus] >> 4 != color)
            {
                check(InputBoard, PositionX - 1, PositionY + 1 * colorminus);
            }
        }
    }
    public bool IsPositionIllegal(byte[,] InputBoard, byte color, int KingX, int KingY)
    {
        bool Output = false;

        return Output;
    }
}
