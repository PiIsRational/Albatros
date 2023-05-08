using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.IO;

class Treesearch
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
     * If Moove Format is int[3] then special Moove involving two pieces
     * Unmove has the format X_a , Y_a , Piece_a , X_b , Y_b , Piece_b
     */

    public standart_chess chess_stuff = new standart_chess();
    public Classic_Eval eval = new Classic_Eval();
    public NNUE_avx2 ValueNet;
    public AlphaBeta alphaBeta = new AlphaBeta(100);
    public Semaphore StopSemaphore = new Semaphore(1, 1);
    public movegen MoveGenerator = new movegen();
    public Random random;
    bool stop = false;
    bool use_nnue = false;
    public bool wasStopped = false;

    public Treesearch(int seed, bool LoadNet ,int ThreadCount)
    {
        random = new Random(seed);
        ValueNet = new NNUE_avx2(LoadNet);
        use_nnue = LoadNet;
    }

    public void SetNet(string File)
    {
        ValueNet.LoadNetFile(File);
    }
    public MCTSimOutput MonteCarloTreeSim(Position board, int NodeCount, bool NewTree, bool WeightedRandom, bool Random, bool NNUE, float c_puct, bool alpha_beta, int depth, bool opening, int[] movelist, ReverseMove undo_move)
    {
        MCTSimOutput Output = new MCTSimOutput();
        AlphaBetaOutput ab_tree_out = new AlphaBetaOutput();

        if(opening)
        {
            movelist = MoveGenerator.LegalMoveGenerator(board, MoveGenerator.check(board, false), undo_move, movelist);
            List<double> score_list = new List<double>();
            int best_score_place = 0;
            byte newcolor = (byte)(board.color ^ 1);

            for (int i = 0; i < MoveGenerator.moveIdx; i++) 
            {
                board = MoveGenerator.make_move(board, movelist[i], true, undo_move);

                score_list.Add(chess_stuff.convert_millipawn_to_wdl(-eval.PestoEval(board)));

                board = MoveGenerator.unmake_move(board, undo_move);
            }
            if (score_list.Count != 0)
            {
                best_score_place = RandomWeightedChooser(score_list, 0.1);
                Output.Position = alphaBeta.play_move(board, movelist[best_score_place], false, null);
                Output.eval = (float)score_list[best_score_place];
              
            }
            else
            {
                Output.eval = 1;
                Output.Position = board;
            }
        }
        else if (alpha_beta)
        {

            ab_tree_out = alphaBeta.selfplay_iterative_deepening(board, depth, NNUE);
            if (ab_tree_out.draw)
            {
                Output.draw = true;
                Output.Position = board;
                Output.eval = 0;
            }
            else
            {
                Output.is_quiet = ab_tree_out.is_quiet;
                if (ab_tree_out.movelist.Count != 0)
                    Output.Position = alphaBeta.play_move(board, ab_tree_out.movelist[0], false, null);
                else
                    Output.Position = board;
                Output.eval = ab_tree_out.Score;
            }
            
        }

        return Output;
    }

    public int RandomWeightedChooser(List<double> Input, double temperature)
    {
        int counter = 0;
        double Denominator = 0;
        double LastValue = 0;

        foreach (double Value in Input)
        {
            Denominator += Math.Pow((Value + 1) / 2, 1 / temperature);
            if (Value == 1)
                return counter;

            counter++;
        }

        double[] ValueList = new double[counter];
        counter = 0;

        foreach (double Value in Input)
        {
            ValueList[counter] = Math.Pow((Value + 1) / 2, 1 / temperature) + LastValue;
            LastValue = ValueList[counter];
            counter++;
        }

        double RamdomDouble = random.NextDouble();

        for (int i = 0; i < ValueList.Length; i++)
            if (RamdomDouble < ValueList[i] / Denominator)
                return i;

        return 0;
    }

    public void SetStop(bool Input)
    {
        StopSemaphore.WaitOne();
        stop = Input;
        StopSemaphore.Release();
    }
    public bool CompareBoards(byte[,] Board1 , byte[,] Board2)
    {
        for(int i = 0; i < 9; i++)
        {
            for (int j = 0; j < 9; j++)
            {
                if (Board1[i, j] != Board2[i, j])
                    return false;
            }
        }
        return true;
    }
}

class MCTSimOutput
{
    public bool is_quiet = false;
    public bool draw = false;
    public Position Position;
    public float eval;
}