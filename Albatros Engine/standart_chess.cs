using System;
using System.Collections.Generic;
using System.Text;

class standart_chess
{
    int mate_value = 20000;
    standart stuff = new standart();

    public bool is_capture(int[] move, byte[,] board)
    {
        if ((move.Length != 5 || (board[move[0], move[1]] & 0b00001111) == 0b00000011) && board[move[2], move[3]] != 0)
            return true;

        return false;
    }
    public bool is_tactical(int[] move , byte[,] board)
    {
        if ((move.Length != 5 || (board[move[0], move[1]] & 0b00001111) >> 1 != 0b11) && board[move[2], move[3]] != 0)
            return true;

        return false;
    }
    public int convert_wdl_to_millipawn(float input)
    {
        return Math.Min(mate_value - 1, Math.Max(1 - mate_value, (int)Math.Round(stuff.inverse_sigmoid(input, 4.2f) * 1000)));
    }
    public float convert_millipawn_to_wdl(int input)
    {
        return stuff.sigmoid(input / 1000, 4.2f);
    }
    public Accumulator acc_copy(Accumulator input)
    {
        Accumulator ouput = new Accumulator(input.Acc[0].Length);

        Array.Copy(input.Acc[0], ouput.Acc[0], input.Acc[0].Length);
        Array.Copy(input.Acc[1], ouput.Acc[1], input.Acc[1].Length);

        return ouput;
    }
    public bool is_castelling(int[] move , byte[,] board)
    {
        if (move.Length == 5 && (board[move[0], move[1]] & 0b00001111) == 0b110)
            return true;

        return false;
    }
    public void display_board(byte[,] InputBoard, byte color)
    {
        string spacer = "+---+---+---+---+---+---+---+---+";
        string backrow = "  a   b   c   d   e   f   g   h";
        string[] rows = new string[8];

        if (color == 0)
            Console.WriteLine("\nBlack To Play\n");
        else
            Console.WriteLine("\nWhite To Play\n");

        for (int i = 1; i < 9; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                if (InputBoard[i, j + 1] != 0)
                {
                    switch (InputBoard[i, j + 1])
                    {
                        case 0b00000001:
                            rows[j] += "| p ";
                            break;
                        case 0b00000011:
                            rows[j] += "| p ";
                            break;
                        case 0b00000010:
                            rows[j] += "| p ";
                            break;
                        case 0b00000100:
                            rows[j] += "| n ";
                            break;
                        case 0b00000101:
                            rows[j] += "| b ";
                            break;
                        case 0b00000110:
                            rows[j] += "| k ";
                            break;
                        case 0b00000111:
                            rows[j] += "| k ";
                            break;
                        case 0b00001000:
                            rows[j] += "| q ";
                            break;
                        case 0b00001001:
                            rows[j] += "| r ";
                            break;
                        case 0b00001010:
                            rows[j] += "| r ";
                            break;
                        case 0b00010001:
                            rows[j] += "| P ";
                            break;
                        case 0b00010010:
                            rows[j] += "| P ";
                            break;
                        case 0b00010011:
                            rows[j] += "| P ";
                            break;
                        case 0b00010100:
                            rows[j] += "| N ";
                            break;
                        case 0b00010101:
                            rows[j] += "| B ";
                            break;
                        case 0b00010110:
                            rows[j] += "| K ";
                            break;
                        case 0b00010111:
                            rows[j] += "| K ";
                            break;
                        case 0b00011000:
                            rows[j] += "| Q ";
                            break;
                        case 0b00011001:
                            rows[j] += "| R ";
                            break;
                        case 0b00011010:
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
    public string generate_fen_from_position(byte[,] position, byte color, int fifty_move_rule)
    {
        string fen_output = "";
        int en_passent_x = 0, en_passent_y = 0;
        bool castle_W_K = false, castle_W_Q = false, castle_B_K = false, castle_B_Q = false;
        int square_count = 0;
        char[] Numbers = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8' };
        char[] Letters = new char[] { '0', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h' };

        for (int j = 8; j > 0; j--)
        {
            for (int i = 1; i < 9; i++)
            {
                if (position[i, j] == 0)
                    square_count++;
                else if (square_count != 0)
                {
                    fen_output += Convert.ToString(square_count);
                    square_count = 0;
                }
                switch (position[i, j])
                {
                    case 0b00000001:
                        fen_output += "p";
                        break;
                    case 0b00000010:
                        fen_output += "p";
                        en_passent_x = i;
                        en_passent_y = j;
                        break;
                    case 0b00000011:
                        fen_output += "p";
                        break;
                    case 0b00000100:
                        fen_output += "n";
                        break;
                    case 0b00000101:
                        fen_output += "b";
                        break;
                    case 0b00000110:
                        fen_output += "k";
                        break;
                    case 0b00000111:
                        fen_output += "k";
                        break;
                    case 0b00001000:
                        fen_output += "q";
                        break;
                    case 0b00001001:
                        fen_output += "r";
                        if (i == 1)
                            castle_B_Q = true;
                        else if (i == 8)
                            castle_B_K = true;
                        break;
                    case 0b00001010:
                        fen_output += "r";
                        break;

                    case 0b00010001:
                        fen_output += "P";
                        break;
                    case 0b00010010:
                        fen_output += "P";
                        en_passent_x = i;
                        en_passent_y = j;
                        break;
                    case 0b00010011:
                        fen_output += "P";
                        break;
                    case 0b00010100:
                        fen_output += "N";
                        break;
                    case 0b00010101:
                        fen_output += "B";
                        break;
                    case 0b00010110:
                        fen_output += "K";
                        break;
                    case 0b00010111:
                        fen_output += "K";
                        break;
                    case 0b00011000:
                        fen_output += "Q";
                        break;
                    case 0b00011001:
                        fen_output += "R";
                        if (i == 1)
                            castle_W_Q = true;
                        else if (i == 8)
                            castle_W_K = true;
                        break;
                    case 0b00011010:
                        fen_output += "R";
                        break;
                }
            }
            if (square_count != 0)
                fen_output += Convert.ToString(square_count);
            square_count = 0;
            if (j != 1)
                fen_output += "/";
        }

        fen_output += color == 0 ? " b " : " w ";

        if (castle_W_K)
            fen_output += "K";
        if (castle_W_Q)
            fen_output += "Q";
        if (castle_B_K)
            fen_output += "k";
        if (castle_B_Q)
            fen_output += "q";
        if (!castle_B_K && !castle_B_Q && !castle_W_K && !castle_W_Q)
            fen_output += "- ";

        if (en_passent_x != 0)
            fen_output += " " + Convert.ToString(Letters[en_passent_x]) + Convert.ToString(Numbers[en_passent_y]) + " ";
        else
            fen_output += " - ";

        fen_output += fifty_move_rule + " 0";

        return fen_output;
    }
}

