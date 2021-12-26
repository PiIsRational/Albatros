using System;
using System.Collections.Generic;
using System.Text;


class Classic_Eval
{
    PSQT psqt = new PSQT();
    public float PestoEval(byte[,] InputBoard , byte Color)
    {
        float MiddleGameValue = 0;
        float EndGameValue = 0;
        float GamePhase = 0;
        float MiddelGamePhase = 0;
        float EndGamePhase = 0;
        for (int i = 1; i < 9; i++)
        {
            for (int j = 1; j < 9; j++)
            {
                int currentColor = InputBoard[i, j] >> 4;
                float ColorMinus = 1;
                if (currentColor == 0)
                    ColorMinus = -1;
                switch (InputBoard[i, j] - (InputBoard[i, j] >> 4) * 0b10000)
                {
                    //PawnStart
                    case 0b00000001:
                        EndGameValue += ColorMinus * (0.94f + psqt.PawnEG[Exchange(j, currentColor), i] / 100);
                        MiddleGameValue += ColorMinus * (0.82f + psqt.PawnMG[Exchange(j, currentColor), i] / 100);
                        break;
                    //PawnEnPassent
                    case 0b00000010:
                        EndGameValue += ColorMinus * (0.94f + psqt.PawnEG[Exchange(j, currentColor), i] / 100);
                        MiddleGameValue += ColorMinus * (0.8f + psqt.PawnMG[Exchange(j, currentColor),i] / 100);
                        break;
                    //NormalPawn
                    case 0b00000011:
                        EndGameValue += ColorMinus * (0.94f + psqt.PawnEG[Exchange(j, currentColor), i] / 100);
                        MiddleGameValue += ColorMinus * (0.82f + psqt.PawnMG[Exchange(j, currentColor),i] / 100);
                        break;
                    //Knight
                    case 0b00000100:
                        GamePhase += 1;
                        EndGameValue += ColorMinus * (2.81f + psqt.KnightEG[Exchange(j, currentColor), i] / 100);
                        MiddleGameValue += ColorMinus * (3.37f + psqt.KnightMG[Exchange(j, currentColor), i] / 100);
                        break;
                    //Bishop
                    case 0b00000101:
                        GamePhase += 1;
                        EndGameValue += ColorMinus * (2.97f + psqt.BishopEG[Exchange(j, currentColor), i] / 100);
                        MiddleGameValue += ColorMinus * (3.65f + psqt.BishopMG[Exchange(j, currentColor),i] / 100);
                        break;
                    //Queen
                    case 0b00001000:
                        GamePhase += 4;
                        EndGameValue += ColorMinus * (9.36f + psqt.QueenEG[Exchange(j, currentColor), i] / 100);
                        MiddleGameValue += ColorMinus * (10.25f + psqt.QueenMG[Exchange(j, currentColor), i] / 100);
                        break;
                    //RookCanCastle
                    case 0b00001001:
                        GamePhase += 2;
                        EndGameValue += ColorMinus * (5.12f + psqt.RookEG[Exchange(j, currentColor), i] / 100);
                        MiddleGameValue += ColorMinus * (4.77f + psqt.RookMG[Exchange(j, currentColor),i] / 100);
                        break;
                    //Normal Rook
                    case 0b00001010:
                        GamePhase += 2;
                        EndGameValue += ColorMinus * (5.12f + psqt.RookEG[Exchange(j, currentColor), i] / 100);
                        MiddleGameValue += ColorMinus * (4.77f + psqt.RookMG[Exchange(j, currentColor), i] / 100);
                        break;
                    //King Can Castle
                    case 0b00000110:
                        EndGameValue += ColorMinus * (psqt.KingEG[Exchange(j, currentColor), i] / 100);
                        MiddleGameValue += ColorMinus * psqt.KingMG[Exchange(j, currentColor), i] / 100;
                        break;
                    //Normal King
                    case 0b00000111:
                        EndGameValue += ColorMinus * (psqt.KingEG[Exchange(j, currentColor), i] / 100 );
                        MiddleGameValue += ColorMinus * psqt.KingMG[Exchange(j, currentColor), i] / 100;
                        break;
                }
            }
        }
        MiddelGamePhase = GamePhase;
        if (MiddelGamePhase > 24)
            MiddelGamePhase = 24;
        EndGamePhase = 24 - MiddelGamePhase;
        return (-2 * Color + 1) * LargeSigmoid(((MiddelGamePhase * MiddleGameValue + EndGamePhase * EndGameValue) / 24), 50);
    }
    public float LargeSigmoid(float Input , int Size)
    {
        return Input / (float)Math.Sqrt((Input / Size) * (Input / Size) + 1);
    }
    public int Exchange(int Y ,int Color )
    {
        if (Color == 0)
            return Y;
        else
            return -Y + 9;
    }
}

public class PSQT
{
    public float[,] PawnMG = new float[9, 9] {
        {0 ,   0,   0,   0,  0,   0,   0,   0,   0 },
        {0 ,   0,   0,   0,  0,   0,   0,   0,   0 },
        {0 ,  98, 134,  61,  95,  68, 126, 34, -11 },
        {0 ,  -6,   7,  26,  31,  65,  56, 25, -20 },
        {0 , -14,  13,   6,  21,  23,  12, 17, -23 },
        {0 , -27,  -2,  -5,  12,  17,   6, 10, -25 },
        {0 , -26,  -4,  -4, -10,   3,   3, 33, -12 },
        {0 , -35,  -1, -20, -23, -15,  24, 38, -22 },
        {0 ,   0,   0,   0,   0,   0,   0,  0,   0 },
    };
    public float[,] PawnEG = new float[9, 9] {
        {0 ,   0,   0,   0,   0,   0,   0,   0,   0},
        {0 ,   0,   0,   0,   0,   0,   0,   0,   0},
        {0 , 178, 173, 158, 134, 147, 132, 165, 187},
        {0 ,  94, 100,  85,  67,  56,  53,  82,  84},
        {0 ,  32,  24,  13,   5,  -2,   4,  17,  17},
        {0 ,  13,   9,  -3,  -7,  -7,  -8,   3,  -1},
        {0 ,   4,   7,  -6,   1,   0,  -5,  -1,  -8},
        {0 ,  13,   8,   8,  10,  13,   0,   2,  -7},
        {0 ,   0,   0,   0,   0,   0,   0,   0,   0},
    };
    public float[,] KnightMG = new float[9, 9] {
        {0 ,    0,   0,   0,   0,   0,   0,   0,    0},
        {0 , -167, -89, -34, -49,  61, -97, -15, -107},
        {0 ,  -73, -41,  72,  36,  23,  62,   7,  -17},
        {0 ,  -47,  60,  37,  65,  84, 129,  73,   44},
        {0 ,   -9,  17,  19,  53,  37,  69,  18,   22},
        {0 ,  -13,   4,  16,  13,  28,  19,  21,   -8},
        {0 ,  -23,  -9,  12,  10,  19,  17,  25,  -16},
        {0 ,  -29, -53, -12,  -3,  -1,  18, -14,  -19},
        {0 , -105, -21, -58, -33, -17, -28, -19,  -23},
    };
    public float[,] KnightEG = new float[9, 9] {
        {0 ,   0,   0,   0,   0,   0,   0,   0,  0 },
        {0 , -58, -38, -13, -28, -31, -27, -63, -99},
        {0 , -25,  -8, -25,  -2,  -9, -25, -24, -52},
        {0 , -24, -20,  10,   9,  -1,  -9, -19, -41},
        {0 , -17,   3,  22,  22,  22,  11,   8, -18},
        {0 , -18,  -6,  16,  25,  16,  17,   4, -18},
        {0 , -23,  -3,  -1,  15,  10,  -3, -20, -22},
        {0 , -42, -20, -10,  -5,  -2, -20, -23, -44},
        {0 , -29, -51, -23, -15, -22, -18, -50, -64},
    };
    public float[,] BishopMG = new float[9, 9] {
        {0 ,   0,   0,   0,   0,   0,   0,   0,   0},
        {0 , -29,   4, -82, -37, -25, -42,   7,  -8},
        {0 , -26,  16, -18, -13,  30,  59,  18, -47},
        {0 , -16,  37,  43,  40,  35,  50,  37,  -2},
        {0 ,  -4,   5,  19,  50,  37,  37,   7,  -2},
        {0 ,  -6,  13,  13,  26,  34,  12,  10,   4},
        {0 ,   0,  15,  15,  15,  14,  27,  18,  10},
        {0 ,   4,  15,  16,   0,   7,  21,  33,   1},
        {0 , -33,  -3, -14, -21, -13, -12, -39, -21},
    };
    public float[,] BishopEG = new float[9, 9] {
        {0 ,   0,   0,   0,   0,   0,   0,   0,  0},
        {0 , -14, -21, -11,  -8, -7,  -9, -17, -24},
        {0 ,  -8,  -4,   7, -12, -3, -13,  -4, -14},
        {0 ,   2,  -8,   0,  -1, -2,   6,   0,   4},
        {0 ,  -3,   9,  12,   9, 14,  10,   3,   2},
        {0 ,  -6,   3,  13,  19,  7,  10,  -3,  -9},
        {0 , -12,  -3,   8,  10, 13,   3,  -7, -15},
        {0 , -14, -18,  -7,  -1,  4,  -9, -15, -27},
        {0 , -23,  -9, -23,  -5, -9, -16,  -5, -17},
    };
    public float[,] RookMG = new float[9, 9] {
        {0 ,   0,   0,   0,   0,  0,  0,   0,   0},
        {0 ,  32,  42,  32,  51, 63,  9,  31,  43},
        {0 ,  27,  32,  58,  62, 80, 67,  26,  44},
        {0 ,  -5,  19,  26,  36, 17, 45,  61,  16},
        {0 , -24, -11,   7,  26, 24, 35,  -8, -20},
        {0 , -36, -26, -12,  -1,  9, -7,   6, -23},
        {0 , -45, -25, -16, -17,  3,  0,  -5, -33},
        {0 , -44, -16, -20,  -9, -1, 11,  -6, -71},
        {0 , -19, -13,   1,  17, 16,  7, -37, -26},
    };
    public float[,] RookEG = new float[9, 9] {
        {0 ,  0,  0,  0,  0,  0,   0,   0,   0},
        {0 , 13, 10, 18, 15, 12,  12,   8,   5},
        {0 , 11, 13, 13, 11, -3,   3,   8,   3},
        {0 ,  7,  7,  7,  5,  4,  -3,  -5,  -3},
        {0 ,  4,  3, 13,  1,  2,   1,  -1,   2},
        {0 ,  3,  5,  8,  4, -5,  -6,  -8, -11},
        {0 , -4,  0, -5, -1, -7, -12,  -8, -16},
        {0 , -6, -6,  0,  2, -9,  -9, -11,  -3},
        {0 , -9,  2,  3, -1, -5, -13,   4, -20},
    };
    public float[,] QueenMG = new float[9, 9] {
        {0 ,   0,   0,   0,   0,   0,   0,   0,  0 },
        {0 , -28,   0,  29,  12,  59,  44,  43,  45},
        {0 , -24, -39,  -5,   1, -16,  57,  28,  54},
        {0 , -13, -17,   7,   8,  29,  56,  47,  57},
        {0 , -27, -27, -16, -16,  -1,  17,  -2,   1},
        {0 ,  -9, -26,  -9, -10,  -2,  -4,   3,  -3},
        {0 , -14,   2, -11,  -2,  -5,   2,  14,   5},
        {0 , -35,  -8,  11,   2,   8,  15,  -3,   1},
        {0 , -1, -18,  -9,  10, -15, -25, -31, -50,},
    };
    public float[,] QueenEG = new float[9, 9] {
        {0 ,   0,   0,   0,   0,   0,   0,   0,  0 },
        {0 ,  -9,  22,  22,  27,  27,  19,  10,  20},
        {0 , -17,  20,  32,  41,  58,  25,  30,   0},
        {0 , -20,   6,   9,  49,  47,  35,  19,   9},
        {0 ,   3,  22,  24,  45,  57,  40,  57,  36},
        {0 , -18,  28,  19,  47,  31,  34,  39,  23},
        {0 , -16, -27,  15,   6,   9,  17,  10,   5},
        {0 , -22, -23, -30, -16, -16, -23, -36, -32},
        {0 , -33, -28, -22, -43,  -5, -32, -20, -41},
    };
    public float[,] KingMG = new float[9, 9] {
        {0 ,   0,   0,   0,   0,   0,   0,   0,  0 },
        {0 , -65,  23,  16, -15, -56, -34,   2,  13},
        {0 ,  29,  -1, -20,  -7,  -8,  -4, -38, -29},
        {0 ,  -9,  24,   2, -16, -20,   6,  22, -22},
        {0 , -17, -20, -12, -27, -30, -25, -14, -36},
        {0 , -49,  -1, -27, -39, -46, -44, -33, -51},
        {0 , -14, -14, -22, -46, -44, -30, -15, -27},
        {0 ,   1,   7,  -8, -64, -43, -16,   9,   8},
        {0 , -15,  36,  12, -54,   8, -28,  24,  14},
    };
    public float[,] KingEG = new float[9, 9] {
        {0 ,   0,   0,   0,   0,   0,   0,   0,  0 },
        {0 , -74, -35, -18, -18, -11,  15,   4, -17},
        {0 , -12,  17,  14,  17,  17,  38,  23,  11},
        {0 ,  10,  17,  23,  15,  20,  45,  44,  13},
        {0 ,  -8,  22,  24,  27,  26,  33,  26,   3},
        {0 , -18,  -4,  21,  24,  27,  23,   9, -11},
        {0 , -19,  -3,  11,  21,  23,  16,   7,  -9},
        {0 , -27, -11,   4,  13,  14,   4,  -5, -17},
        {0 , -53, -34, -21, -11, -28, -14, -24, -43},
    };
}

