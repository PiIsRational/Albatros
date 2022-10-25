using System;
using System.Collections.Generic;
using System.Diagnostics;
class Io
{
    string CurrentCommand = "";
    standart stuff = new standart();
    standart_chess chess_stuff = new standart_chess();
    Game game = new Game();
    public int[] Move = new int[2];
    Treesearch treesearch = new Treesearch(1245845, true, 5);
    Stopwatch sw = new Stopwatch();
    public AlphaBeta AlphaBetaSearch;
    int movestogo = 40, played_moves = 0;
    bool movelimit = false;
    public volatile bool stop = false;
    public Io()
    {
        AlphaBetaSearch = new AlphaBeta(game.HashSize);
    }
    public void SetCurrentCommand(string Input)
    {
        CurrentCommand = Input;
    }
    public void ThreadStart()
    {
        game.SetOutput(CommandRecon(CurrentCommand));
        CurrentCommand = "";
    }
    public void Stop()
    {
        AlphaBetaSearch.Stop();
        treesearch.SetStop(true);
    }
    public string CommandRecon(string command)
    {
        string output = "";
        string[] command_syntax = SyntaxWithoutHoles(command.Split(' '));
        if (command_syntax.Length > 0)
        {
            switch (command_syntax[0])
            {
                case "":
                    break;
                case "test":
                    break;
                case "d":
                    print_board_and_info(game.board);
                    break;
                case "uci":
                    Console.WriteLine(
                        "\nid name Albatros" +
                        "\nid author D.Grevent" +
                        "\n" +
                        "\noption name Hash type spin default 18 min 1 max 10000"+
                        "\noption name c_puct type string default 1" +
                        "\noption name EvalFile type string default ValueNet.nnue" +
                        "\noption name Use NNUE type check default true" +
                        "\noption name Threads type spin default 1 min 1 max 1" +
                        "\n" +
                        "\nuciok") ;
                    break;
                case "ucinewgame":
                    LoadPositionBoard();
                    break;
                case "eval":
                    ReturnEvaluation(game.board);
                    break;
                case "go":
                    if(command_syntax.Length >= 2)
                    {
                        switch (command_syntax[1])
                        {
                            case "winc":
                                Move = PlanMoveTime(command_syntax, game.board,game.NNUE, game.USE_MCTS);
                                if (Move != null)
                                    ReturnMove(Move[0], Move[1]);
                                break;
                            case "binc":
                                Move = PlanMoveTime(command_syntax, game.board, game.NNUE, game.USE_MCTS);
                                if (Move != null)
                                    ReturnMove(Move[0], Move[1]);
                                break;
                            case "btime":
                                Move = PlanMoveTime(command_syntax, game.board,  game.NNUE, game.USE_MCTS);
                                if (Move != null)
                                    ReturnMove(Move[0], Move[1]);
                                break;
                            case "wtime":
                                Move = PlanMoveTime(command_syntax, game.board,  game.NNUE, game.USE_MCTS);
                                if (Move != null)
                                    ReturnMove(Move[0], Move[1]);
                                break;
                            case "movestogo":
                                Move = PlanMoveTime(command_syntax, game.board, game.NNUE, game.USE_MCTS);
                                if (Move != null)
                                    ReturnMove(Move[0], Move[1]);
                                break;
                            case "ponder":
                                if (game.USE_MCTS)
                                {
                                    //Move = treesearch.MultithreadMcts(game.board,Int32.MaxValue, game.NNUE, game.ThreadCount, true, false, 0, game.c_puct);
                                    if (Move != null)
                                        ReturnMove(Move[0], Move[1]);
                                }
                                else
                                {
                                    Move[0] = AlphaBetaSearch.iterative_deepening(game.board, AlphaBeta.max_depth, game.NNUE, false);
                                    ReturnMove(Move[0], int.MaxValue);
                                }
                                break;
                            case "infinite":
                                if (game.USE_MCTS)
                                {
                                    //Move = treesearch.MultithreadMcts(game.board,  Int32.MaxValue, game.NNUE, game.ThreadCount, true, false, 0, game.c_puct);
                                    if (Move != null)
                                        ReturnMove(Move[0], Move[1]);
                                }
                                else
                                {
                                    Move[0] = AlphaBetaSearch.iterative_deepening(game.board, AlphaBeta.max_depth, game.NNUE, false);
                                    ReturnMove(Move[0], int.MaxValue);
                                }
                                break;
                            case "movetime":
                                if (game.USE_MCTS)
                                {
                                    //Move = treesearch.MultithreadMcts(game.board, Int32.MaxValue, game.NNUE, game.ThreadCount, true, true, Convert.ToInt64(command_syntax[2]), game.c_puct);
                                    if (Move != null)
                                        ReturnMove(Move[0], Move[1]);
                                }
                                else
                                {
                                    Move[0] = AlphaBetaSearch.TimedAlphaBeta(Convert.ToInt64(command_syntax[2]), game.board,  game.NNUE, false);
                                    ReturnMove(Move[0], int.MaxValue);
                                }
                                break;
                            case "perft":
                                try
                                {
                                    perft_out(Convert.ToInt32(command_syntax[2]), game.board);
                                }
                                catch
                                {
                                    Console.WriteLine("there was an Error!\n");
                                }
                                break;
                            case "depth":
                                try
                                {
                                    Move[0] = AlphaBetaSearch.iterative_deepening(game.board, Convert.ToInt32(command_syntax[2]), game.NNUE, false);
                                    ReturnMove(Move[0], int.MaxValue);
                                }
                                catch
                                {
                                    Console.WriteLine("there was an Error!\n");
                                }
                                break;
                            case "nodes":
                                //Move = treesearch.MultithreadMcts(game.board,  Convert.ToInt32(command_syntax[2]), game.NNUE, game.ThreadCount, false, false, 0, game.c_puct);
                                if (Move != null)
                                    ReturnMove(Move[0], Move[1]);
                                break;
                            default:
                                if (game.USE_MCTS)
                                {
                                    //Move = treesearch.MultithreadMcts(game.board, Int32.MaxValue, game.NNUE, game.ThreadCount, true, false, 0, game.c_puct);
                                    if (Move != null)
                                        ReturnMove(Move[0], Move[1]);
                                }
                                else
                                {
                                    Move[0] = AlphaBetaSearch.iterative_deepening(game.board,  AlphaBeta.max_depth, game.NNUE, false);
                                    ReturnMove(Move[0], int.MaxValue);
                                }
                                break;
                        }
                    }
                    else
                    {
                        if (game.USE_MCTS)
                        {
                            //Move = treesearch.MultithreadMcts(game.board, Int32.MaxValue, game.NNUE, game.ThreadCount, true, false, 0, game.c_puct);
                            if (Move != null)
                                ReturnMove(Move[0], Move[1]);
                        }
                        else
                        {
                            Move[0] = AlphaBetaSearch.iterative_deepening(game.board,  AlphaBeta.max_depth, game.NNUE, false);
                            ReturnMove(Move[0], int.MaxValue);
                        }
                    }
                    break;
                case "setoption":
                    if (command_syntax[1] == "name")
                    {
                        switch (command_syntax[2])
                        {
                            case "Ponder":
                                break;
                            case "EvalFile":
                                try
                                {
                                    treesearch.SetNet(command_syntax[4]);
                                    game.NetName = command_syntax[4];
                                }
                                catch
                                {
                                    Console.WriteLine("No such File: " + command_syntax[4] + "\n");
                                }
                                break;
                            case "c_puct":
                                try
                                {
                                    game.c_puct = Convert.ToSingle(command_syntax[4]);
                                    if (game.c_puct < 0)
                                        game.c_puct = 0;
                                }
                                catch
                                {
                                    Console.WriteLine("{0} is not a number \n", command_syntax[4]);
                                }
                                break;
                            case "Use":
                                if (command_syntax[3] == "NNUE")
                                {
                                    if (command_syntax[5] == "true")
                                        game.NNUE = true;
                                    else if (command_syntax[5] == "false")
                                        game.NNUE = false;
                                }
                                break;                      
                            case "Threads":
                                try
                                {
                                    game.ThreadCount = Convert.ToInt32(command_syntax[4]) - 1;
                                    if (game.ThreadCount < 1)
                                        game.ThreadCount = 1;
                                    //treesearch.ChangeThreadCount(game.ThreadCount);
                                }
                                catch { Console.WriteLine("{0} is not a number \n", command_syntax[4]); }
                                break;
                            case "Hash":
                                try
                                {
                                    game.HashSize = Convert.ToInt32(command_syntax[4]);
                                    game.HashSize = Math.Max(Math.Min(game.HashSize, 10000), 1);
                                    AlphaBetaSearch.HashTable = new byte[game.HashSize * 55556, 18];
                                }
                                catch { Console.WriteLine("{0} is not a number \n", command_syntax[4]); }
                                break;
                            case "LMR_debug":
                                AlphaBetaSearch.init_reductions(Convert.ToInt32(command_syntax[4]), Convert.ToInt32(command_syntax[5]), Convert.ToInt32(command_syntax[6]), Convert.ToInt32(command_syntax[7]),
                                    Convert.ToDouble(command_syntax[8]), Convert.ToDouble(command_syntax[9]), Convert.ToDouble(command_syntax[10]), Convert.ToDouble(command_syntax[11]));
                                break;
                        }
                    }
                    break;
                case "position":
                    switch (command_syntax[1])
                    {
                        case "startpos":
                            reset_board();
                            try
                            {
                                if (command_syntax[2] == "moves")
                                {
                                    string[] move_commands = new string[command_syntax.Length - 3];

                                    for (int i = 3; i < command_syntax.Length; i++)
                                        move_commands[i - 3] = command_syntax[i];

                                    game.board = PlayGameFromCommand(move_commands, false);
                                }
                            }
                            catch
                            { }
                            break;
                        case "fen":
                            try
                            {
                                reset_board();
                                game.board = chess_stuff.LoadPositionFromFen(game.board, command_syntax[2] + " " + command_syntax[3] + " " + command_syntax[4] + " " + command_syntax[5]);
                                try
                                {
                                    for (int i = 5; i < command_syntax.Length; i++)
                                    {
                                        if (command_syntax[i] == "moves")
                                        {
                                            string[] MoveComands = new string[command_syntax.Length - (i + 1)];

                                            for (int j = i + 1; j < command_syntax.Length; j++)
                                                MoveComands[j - (i + 1)] = command_syntax[j];

                                            game.board = PlayGameFromCommand(MoveComands, false);
                                        }
                                    }

                                }
                                catch
                                { }
                            }
                            catch
                            {
                                Console.WriteLine("there was an Error!\n");
                            }
                            break;
                        default:
                            Console.WriteLine("Unknown command: " + command + "\n");
                            break;
                    }
                    break;
                case "isready":
                    Console.WriteLine("readyok");
                    break;
                default:
                    Console.WriteLine("Unknown command: " + command + "\n");
                    break;
            }
        }
        return output;
    }
    public int[] PlanMoveTime(string[] Command, position board, bool NNUE , bool USE_MCTS)
    {
        int wtime = 0, btime = 0, winc = 0, binc = 0;
        int timeMe = 0, TimeEnemy = 0, Meinc = 0, Enemyinc = 0;
        long timeToUse = 0;
        bool change_time = true;
        for (int i = 0; i < Command.Length - 1; i++)
        {
            switch(Command[i])
            {
                case "wtime":
                    wtime = Convert.ToInt32(Command[i + 1]);
                    break;
                case "btime":
                    btime = Convert.ToInt32(Command[i + 1]);
                    break;
                case "winc":
                    winc = Convert.ToInt32(Command[i + 1]);
                    break;
                case "binc":
                    binc = Convert.ToInt32(Command[i + 1]);
                    break;
                case "movestogo":
                    movestogo = Convert.ToInt32(Command[i + 1]);
                    movelimit = true;
                    break;
            }
        }
        if(board.color == 0)
        {
            timeMe = btime;
            TimeEnemy = wtime;
            Meinc = binc;
            Enemyinc = winc;
        }
        else
        {
            timeMe = wtime;
            TimeEnemy = btime;
            Meinc = winc;
            Enemyinc = binc;
        }

        timeToUse = Meinc;

        float move_number = Math.Min(played_moves, 10);
        played_moves++;

        if (movestogo <= 10 && !movelimit)
            movestogo += 15;

        timeToUse += (long)(timeMe / (movestogo - 1));
        movestogo--;


        if (USE_MCTS)
        {
            if (timeToUse < 3000)
                return new int[] { AlphaBetaSearch.TimedAlphaBeta(timeToUse - 50, board, NNUE, false), int.MaxValue };
            /*else
                return treesearch.MultithreadMcts(game.board, Int32.MaxValue, game.NNUE, game.ThreadCount, true, true, timeToUse - 500, game.c_puct);*/
        }
        else
            return new int[] { AlphaBetaSearch.TimedAlphaBeta(timeToUse - 50, board, NNUE, change_time), 0 };

        return new int[1];
    }
    public void LoadPositionBoard()
    {
        movestogo = 40;
        played_moves = 0;
        AlphaBetaSearch.time_to_use = 0;
        game.Playing = false;
        treesearch.CurrentTree = null;
        AlphaBetaSearch.HashTable = new byte[game.HashSize * 62500, 16];
        game.board = chess_stuff.LoadPositionFromFen(game.board, game.StartPosition);
    }
    public void reset_board()
    {
        AlphaBetaSearch.time_to_use = 0;
        AlphaBetaSearch.HashTable = new byte[game.HashSize * 62500, 16];
        game.board = chess_stuff.LoadPositionFromFen(game.board, game.StartPosition);
    }
    public void perft_out(int depth, position board)
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();
        bool in_check = AlphaBetaSearch.MoveGenerator.check(board, false);
        reverse_move move_undo = new reverse_move();
        //instantiate the movelist
        int[][] movelist = new int[depth][];
        for (int i = 0; i < depth; i++)
            movelist[i] = new int[218];
        movelist[depth - 1] = AlphaBetaSearch.MoveGenerator.legal_move_generator(board, in_check, move_undo, movelist[depth - 1]);
        int movelist_length = AlphaBetaSearch.MoveGenerator.move_idx;
        long score = 0, counter = 0;
        for (int i = 0; i < movelist_length; i++)
        {
            board = AlphaBetaSearch.MoveGenerator.make_move(board, movelist[depth - 1][i], true, move_undo);
            if (!AlphaBetaSearch.MoveGenerator.illegal_position)
            {
                if (depth - 1 != 0)
                    score = perft(board, depth - 1, AlphaBetaSearch.MoveGenerator.fast_check(board, movelist[depth - 1][i]), movelist);
                else
                    score = 1;

                if (score != 0)
                    Console.WriteLine("{0}: {1}", chess_stuff.move_to_string(movelist[depth - 1][i]), score);
                counter += score;
            }
            else
                AlphaBetaSearch.MoveGenerator.illegal_position = false;
            board = AlphaBetaSearch.MoveGenerator.unmake_move(board, move_undo);
        }
        Console.WriteLine("{0} nodes explored in {1} millisecondseconds", counter, sw.ElapsedMilliseconds);
        if (stop)
            stop = false;
    }
    public long perft(position board, int depth, bool in_check, int[][] movelist)
    {
        if (stop)
            return 0;

        reverse_move move_undo = new reverse_move();
        movelist[depth - 1] = AlphaBetaSearch.MoveGenerator.legal_move_generator(board, in_check, move_undo, movelist[depth - 1]);

        int movelist_length = AlphaBetaSearch.MoveGenerator.move_idx;

        if (depth == 1)
            return movelist_length;

        long counter = 0;

        for (int i = 0; i < movelist_length; i++)
        {
            board = AlphaBetaSearch.MoveGenerator.make_move(board, movelist[depth - 1][i], true, move_undo);

            if (!AlphaBetaSearch.MoveGenerator.illegal_position)
                counter += perft(board, depth - 1, AlphaBetaSearch.MoveGenerator.fast_check(board, movelist[depth - 1][i]), movelist);
            else
                AlphaBetaSearch.MoveGenerator.illegal_position = false;
            board = AlphaBetaSearch.MoveGenerator.unmake_move(board, move_undo);
        }

        return counter;
    }
    public void ReturnMove(int Move, int PonderMove)
    {
        string[] ConvertNumToLetter = new string[] { "0", "a", "b", "c", "d", "e", "f", "g", "h" };
        string[] Promotion = new string[] { "", "n", "b", "q", "r" };
        string Output = "";

        Output = "bestmove " + chess_stuff.move_to_string(Move);

        if (PonderMove != int.MaxValue)
            Output += " ponder " + chess_stuff.move_to_string(PonderMove);

        Console.WriteLine(Output);
    }
    public void print_board_and_info(position board)
    {
        chess_stuff.display_board(board);
        Console.WriteLine();
        Console.WriteLine("Fen: {0}", chess_stuff.generate_fen_from_position(board));
        Console.WriteLine("Key: {0}", AlphaBetaSearch.hash.hash_position(board));
        Console.WriteLine();
    }
    public string[] SyntaxWithoutHoles(string[] Syntax)
    {
        string[] Output = new string[0];
        foreach (string Word in Syntax)
        {
            if (Word != "" && Word != null)
            {
                Array.Resize(ref Output, Output.Length + 1);
                Output[Output.Length - 1] = Word;
            }
        }
        return Output;
    }
    public position PlayGameFromCommand(string[] Commands , bool TreeUpdate)
    {
        int[] PlayingMoves = new int[Commands.Length];

        for (int i = 0; i < Commands.Length; i++)
        {
           PlayingMoves[i] = chess_stuff.convert_string_to_move(Commands[i]);
        }

        return AlphaBetaSearch.PlayGameFromMoves(game.board, PlayingMoves);
    }
    public string ReturnNumber(float Input)
    {
        string Output = "";
        string Sign = "-";
        Input = (float)Math.Round(Input, 2);
        if (Input >= -0)
            Sign = "+";
        Output = string.Format("{0:G3}", Math.Abs(Input));
        if (Input == 0)
            Output = "0,00";
        if (Output.ToCharArray().Length == 3)
            Output += "0";
        if (Output.ToCharArray().Length == 1)
            Output += ",00";
        return Sign + Output;
    }
    public void ReturnEvaluation(position board)
    {
        if (board.color == 0)
            Console.WriteLine("\nNNUE network contributions (Black to move)\n");
        else
            Console.WriteLine("\nNNUE network contributions (White to move)\n");
        float Value = 0;

        AlphaBetaSearch.ValueNet.set_acc_from_position(board);
        Value = ((float)AlphaBetaSearch.ValueNet.AccToOutput(AlphaBetaSearch.ValueNet.acc, (byte)(board.color)) / 1000);

        Console.WriteLine("NNUE evaluation                  {0} (white side)", ReturnNumber((float)Value));

        Value = (float)treesearch.eval.pesto_eval(board) / 1000;

        Console.WriteLine("Classical evaluation             {0} (white side)\n", ReturnNumber((float)Value));
    }
    public int GetPlaceInArray(char[] Array, char character)
    {
        for (int i = 0; i < Array.Length; i++)
            if (Array[i] == character)
                return i;
        return 0;
    }
    public void SetElo(int Elo)
    {
        game.Elo = Elo;
    }
    public void TrainingStart()
    {
        game.training = new Training(game.board, game.NetName, game.buffer_size, game.GameLength, game.NodeCount, game.learning_rate, game.ThreadCount, game.Momentum, game.batch_size, game.Lambda, game.Play, game.LogFile, game.depthPly);
    }
}
