using System;
using System.Collections.Generic;
using System.Text;

class standart_chess
{
    //squares
    public const byte no_square = byte.MaxValue;

    //pieces
    public const byte no_piece = 0;
    public const byte pawn = 0b00000001;
    public const byte knight = 0b00000010;
    public const byte bishop = 0b00000011;
    public const byte rook = 0b00000100;
    public const byte queen = 0b00000101;
    public const byte king = 0b00000110;

    //colors
    public const byte black = 0;
    public const byte white = 1;

    //promotions and other stuff
    public const byte no_promotion = 0;
    public const byte double_pawn_move = 1;
    public const byte castle_or_en_passent = 2;
    public const byte knight_promotion = 3;
    public const byte bishop_promotion = 4;
    public const byte rook_promotion = 5;
    public const byte queen_promotion = 6;

    public const int mate_value = 60000;
    public const int illegal_position_value = 80000;
    public const int max_depth = 256;

    standart stuff = new standart();
    public Position LoadPositionFromFen(Position board, string Fen)
    {
        board.reset();
        int CurrentDisplacement = 0;
        int displacementCounter = 0;
        char[] casteling;
        int[] EnPassentCoordinate = new int[2];
        char[] EnPassentPawn;
        //First cut the fen into its different parts
        string[] Semantics = Fen.Split(' ');
        //Then cut the Board into files
        string[] Files = Semantics[0].Split('/');

        //Find the coordinates of the enPassent Piece
        if (Semantics[3] != "-")
        {
            EnPassentPawn = Semantics[3].ToCharArray();
            char[] Numbers = new char[] { '1', '2', '3', '4', '5', '6', '7', '8' };
            char[] Letters = new char[] { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h' };
            for (int i = 0; i < 8; i++)
            {
                if (Numbers[i] == EnPassentPawn[1])
                    EnPassentCoordinate[0] = i;

                if (Letters[i] == EnPassentPawn[0])
                    EnPassentCoordinate[1] = i;
            }

            board.en_passent_square = (byte)(EnPassentCoordinate[0] + EnPassentCoordinate[1] * 8);
        }
        else
            board.en_passent_square = byte.MaxValue;

        //Look For Castelling
        if (Semantics[2] != "-")
        {
            casteling = Semantics[2].ToCharArray();
            for (int i = 0; i < casteling.Length; i++)
            {
                if (casteling[i] == 'K')
                {
                    board.rook_not_moved[3] = true;
                    board.king_not_moved[1] = true;
                }
                if (casteling[i] == 'Q')
                {
                    board.rook_not_moved[2] = true;
                    board.king_not_moved[1] = true;
                }
                if (casteling[i] == 'k')
                {
                    board.rook_not_moved[1] = true;
                    board.king_not_moved[0] = true;
                }
                if (casteling[i] == 'q')
                {
                    board.rook_not_moved[0] = true;
                    board.king_not_moved[0] = true;
                }
            }
        }
        //Go trough all the ranks and divide them into pieces
        char[] Pieces;
        for (int j = 0; j < 8; j++)
        {
            Pieces = Files[7 - j].ToCharArray();
            for (int i = 0; i < 8; i++)
            {
                switch (Pieces[i - displacementCounter])
                {
                    case 'K':
                        board = add_piece_to_position(board, (byte)(j * 8 + i), 0b00001110);
                        break;
                    case 'Q':
                        board = add_piece_to_position(board, (byte)(j * 8 + i), 0b00001101);
                        break;
                    case 'N':
                        board = add_piece_to_position(board, (byte)(j * 8 + i), 0b00001010);
                        break;
                    case 'R':
                        board = add_piece_to_position(board, (byte)(j * 8 + i), 0b00001100);
                        break;
                    case 'B':
                        board = add_piece_to_position(board, (byte)(j * 8 + i), 0b00001011);
                        break;
                    case 'p':
                        board = add_piece_to_position(board, (byte)(j * 8 + i), 0b00000001);
                        break;

                    case 'k':
                        board = add_piece_to_position(board, (byte)(j * 8 + i), 0b00000110);
                        break;
                    case 'q':
                        board = add_piece_to_position(board, (byte)(j * 8 + i), 0b00000101);
                        break;
                    case 'n':
                        board = add_piece_to_position(board, (byte)(j * 8 + i), 0b00000010);
                        break;
                    case 'r':
                        board = add_piece_to_position(board, (byte)(j * 8 + i), 0b00000100);
                        break;
                    case 'b':
                        board = add_piece_to_position(board, (byte)(j * 8 + i), 0b00000011);
                        break;
                    case 'P':
                        board = add_piece_to_position(board, (byte)(j * 8 + i), 0b00001001);
                        break;
                    default:
                        CurrentDisplacement = Convert.ToInt32(Pieces[i - displacementCounter]) - 49;
                        i += CurrentDisplacement;
                        displacementCounter += CurrentDisplacement;
                        break;

                }
            }
            displacementCounter = 0;
        }
        //init Turn
        if (Semantics[1] == "w")
            board.color = 1;
        else
            board.color = 0;

        return board;
    }
    public Position add_piece_to_position(Position input, byte square, byte piece)
    {
        input.board[square] = piece;
        input.boards[get_piece_color(piece), square] = (byte)(piece & 0b00000111);

        input.piece_square_lists[piece][input.piececount[piece]] = square;
        input.idx_board[square] = input.piececount[piece];
        input.piececount[piece]++;

        return input;
    }
    public string move_to_string(int move)
    {
        byte other = (byte)(move >> 12);
        byte from = (byte)(move & 0b0000000000111111);
        int to = (byte)((move & 0b0000111111000000) >> 6);
        string[] values = { "1", "2", "3", "4", "5", "6", "7", "8" };
        string[] characters = { "a", "b", "c", "d", "e", "f", "g", "h" };
        string[] promotions = { "", "", "", "n", "b", "r", "q" };
        return characters[from & 0b000111] + values[from >> 3] + characters[to & 0b000111] + values[to >> 3] + promotions[other];
    }
    public int convert_string_to_move(string move_string)
    {
        char[] characters = move_string.ToCharArray();
        byte[] squares = new byte[2];

        for (int i = 0; i < 2; i++)
            squares[i] = (byte)((characters[2 * i] - 97) ^ ((characters[2 * i + 1] - 49) << 3));

        string[] promotions = { "", "0", "0", "n", "b", "r", "q" };
        int promotion_value = 0;
        if (characters.Length == 5)
        {
            for (int i = 0; i < promotions.Length; i++)
            {
                if (promotions[i] == Convert.ToString(characters[4]))
                {
                    promotion_value = i;
                    break;
                }
            }
        }
        return move_x_to_y(squares[0], squares[1], (byte)promotion_value);
    }
    public short move_x_to_y(byte from, byte to, byte extra)
    {
        return (short)(from ^ (to << 6) ^ (extra << 12));
    }
    public void display_board(Position input)
    {
        string spacer = "+---+---+---+---+---+---+---+---+";
        string backrow = "  a   b   c   d   e   f   g   h";
        string[] rows = new string[8];

        if (input.color == 0)
            Console.WriteLine("\nBlack To Play\n");
        else
            Console.WriteLine("\nWhite To Play\n");

        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                if (input.board[i + j * 8] != 0)
                {
                    switch (input.board[i + j * 8])
                    {
                        case 0b00000001:
                            rows[j] += "| p ";
                            break;
                        case 0b00000010:
                            rows[j] += "| n ";
                            break;
                        case 0b00000011:
                            rows[j] += "| b ";
                            break;
                        case 0b00000110:
                            rows[j] += "| k ";
                            break;
                        case 0b00000101:
                            rows[j] += "| q ";
                            break;
                        case 0b00000100:
                            rows[j] += "| r ";
                            break;
                        case 0b00001001:
                            rows[j] += "| P ";
                            break;
                        case 0b00001010:
                            rows[j] += "| N ";
                            break;
                        case 0b00001011:
                            rows[j] += "| B ";
                            break;
                        case 0b00001110:
                            rows[j] += "| K ";
                            break;
                        case 0b00001101:
                            rows[j] += "| Q ";
                            break;
                        case 0b00001100:
                            rows[j] += "| R ";
                            break;
                    }
                }
                else
                    rows[j] += "|   ";

            }
        }
        for (int i = 7; i >= 0; i--)
        {
            Console.WriteLine(spacer + "\n" + rows[i] + "| " + (i + 1));
        }
        Console.WriteLine(spacer + "\n" + backrow);
    }
    public byte get_y_from_square(byte square)
    {
        return (byte)(square >> 3);
    }
    public byte get_x_from_square(byte square)
    {
        return (byte)(square & 0b00000111);
    }
    public int convert_wdl_to_centipawn(float input)
    {
        return (int)Math.Round(stuff.inverse_sigmoid(input, 4.2f) * 100);
    }
    public byte get_piece_color(byte piece)
    {
        return (byte)((piece & 0b000001000) >> 3);
    }
    public string generate_fen_from_position(Position board)
    {
        string fen_output = "";
        int square_count = 0;
        char[] Numbers = new char[] { '1', '2', '3', '4', '5', '6', '7', '8' };
        char[] Letters = new char[] { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h' };

        for (int j = 7; j > -1; j--)
        {
            for (int i = 0; i < 8; i++)
            {
                if (board.board[j * 8 + i] == 0)
                    square_count++;
                else if (square_count != 0)
                {
                    fen_output += Convert.ToString(square_count);
                    square_count = 0;
                }
                switch (board.board[j * 8 + i])
                {
                    case 0b00000001:
                        fen_output += "p";
                        break;
                    case 0b00000010:
                        fen_output += "n";
                        break;
                    case 0b00000011:
                        fen_output += "b";
                        break;
                    case 0b00000100:
                        fen_output += "r";
                        break;
                    case 0b00000101:
                        fen_output += "q";
                        break;
                    case 0b00000110:
                        fen_output += "k";
                        break;

                    case 0b00001001:
                        fen_output += "P";
                        break;
                    case 0b00001010:
                        fen_output += "N";
                        break;
                    case 0b00001011:
                        fen_output += "B";
                        break;
                    case 0b00001100:
                        fen_output += "R";
                        break;
                    case 0b00001101:
                        fen_output += "Q";
                        break;
                    case 0b00001110:
                        fen_output += "K";
                        break;

                }
            }
            if (square_count != 0)
                fen_output += Convert.ToString(square_count);
            square_count = 0;
            if (j != 0)
                fen_output += "/";
        }

        fen_output += board.color == 0 ? " b " : " w ";

        if (board.rook_not_moved[3] && board.king_not_moved[1])
            fen_output += "K";
        if (board.rook_not_moved[2] && board.king_not_moved[1])
            fen_output += "Q";
        if (board.rook_not_moved[1] && board.king_not_moved[0])
            fen_output += "k";
        if (board.rook_not_moved[0] && board.king_not_moved[0])
            fen_output += "q";
        if (((!board.rook_not_moved[3] && !board.rook_not_moved[2]) || !board.king_not_moved[1]) && ((!board.rook_not_moved[1] && !board.rook_not_moved[0]) || !board.king_not_moved[0]))
            fen_output += "-";

        if (board.en_passent_square != byte.MaxValue)
            fen_output += " " + Convert.ToString(Letters[board.en_passent_square % 8]) + Convert.ToString(Numbers[board.en_passent_square / 8]) + " ";
        else
            fen_output += " - ";

        fen_output += board.fifty_move_rule + " 0";

        return fen_output;
    }
    public bool is_capture(int move, Position board)
    {
        /*if we go to an occupied square it is a capture 
         * or if the move is special,
         * we are a normal pawn and it is not a promotion,
         * it is en passent and a capture
         */
        byte to = (byte)((move & 0b0000111111000000) >> 6);
        if (board.board[to] != 0 || is_en_passent(move, board))
            return true;

        return false;
    }
    public bool is_en_passent(int move , Position board)
    {
        /*if the move is special
         * if it is not a promotion
         * and if the piece is a pawn with the ability for en passent*/
        byte other = (byte)(move >> 12);
        if (other == castle_or_en_passent)
        {
            byte to = (byte)((move & 0b0000111111000000) >> 6);
            if (to == board.en_passent_square)
                return true;
        }
        return false;
    }
    public bool non_mate_window(int alpha, int beta)
    {
        return alpha > -mate_value && beta < mate_value;
    }
    public int convert_wdl_to_millipawn(float input)
    {
        return Math.Min(mate_value - 1, Math.Max(1 - mate_value, (int)Math.Round(stuff.inverse_sigmoid(input, 4.2f) * 1000)));
    }
    public float convert_millipawn_to_wdl(int input)
    {
        if (Math.Abs(input) >= mate_value)
            return input / mate_value;
        return stuff.sigmoid((float)input / 1000, 4.2f);
    }
    public Accumulator accCopy(Accumulator input)
    {
        Accumulator ouput = new Accumulator(input.Acc[0].Length);

        Array.Copy(input.Acc[0], ouput.Acc[0], input.Acc[0].Length);
        Array.Copy(input.Acc[1], ouput.Acc[1], input.Acc[1].Length);

        return ouput;
    }
    public bool is_castelling(int move , Position board)
    {
        byte other = (byte)(move >> 12);
        byte from = (byte)(move & 0b0000000000111111);
        if (other == castle_or_en_passent && board.boards[board.color, from] == king)
            return true;

        return false;
    }
}

