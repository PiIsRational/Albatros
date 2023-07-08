using System;
using System.Collections.Generic;
using System.Diagnostics;
class Io
{
    string CurrentCommand = "";
    Standart stuff = new Standart();
    StandartChess chess_stuff = new StandartChess();
    Game game = new Game();
    public int[] move = new int[2];
    Stopwatch sw = new Stopwatch();
    public AlphaBeta alphaBetaSearch;
    int movestogo = 40, played_moves = 0;
    bool movelimit = false;
    public volatile bool stop = false;
    public Io()
    {
        alphaBetaSearch = new AlphaBeta(game.HashSize);
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
        alphaBetaSearch.Stop();
    }
    public string CommandRecon(string command)
    {
        string output = "";
        List<string> commandSyntax = SyntaxWithoutHoles(command.Split(' '));

        if (commandSyntax.Count > 0)
        {
            switch (commandSyntax[0])
            {
                case "":
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
                        "\noption name EvalFile type string default ValueNet.nnue" +
                        "\noption name Use NNUE type check default true" +
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
                    if(commandSyntax.Count >= 2)
                    {
                        switch (commandSyntax[1])
                        {
                            case "winc":
                                move = PlanMoveTime(commandSyntax, game.board,game.NNUE);
                                if (move != null)
                                    ReturnMove(move[0], move[1]);
                                break;
                            case "binc":
                                move = PlanMoveTime(commandSyntax, game.board, game.NNUE);
                                if (move != null)
                                    ReturnMove(move[0], move[1]);
                                break;
                            case "btime":
                                move = PlanMoveTime(commandSyntax, game.board,  game.NNUE);
                                if (move != null)
                                    ReturnMove(move[0], move[1]);
                                break;
                            case "wtime":
                                move = PlanMoveTime(commandSyntax, game.board,  game.NNUE);
                                if (move != null)
                                    ReturnMove(move[0], move[1]);
                                break;
                            case "movestogo":
                                move = PlanMoveTime(commandSyntax, game.board, game.NNUE);
                                if (move != null)
                                    ReturnMove(move[0], move[1]);
                                break;
                            case "ponder":
                                if (game.USE_MCTS)
                                {
                                    if (move != null)
                                        ReturnMove(move[0], move[1]);
                                }
                                else
                                {
                                    move[0] = alphaBetaSearch.IterativeDeepening(game.board, AlphaBeta.MAX_DEPTH, game.NNUE, false);
                                    ReturnMove(move[0], int.MaxValue);
                                }
                                break;
                            case "infinite":
                                if (game.USE_MCTS)
                                {
                                    if (move != null)
                                        ReturnMove(move[0], move[1]);
                                }
                                else
                                {
                                    move[0] = alphaBetaSearch.IterativeDeepening(game.board, AlphaBeta.MAX_DEPTH, game.NNUE, false);
                                    ReturnMove(move[0], int.MaxValue);
                                }
                                break;
                            case "movetime":
                                if (game.USE_MCTS)
                                {
                                    if (move != null)
                                        ReturnMove(move[0], move[1]);
                                }
                                else
                                {
                                    move[0] = alphaBetaSearch.TimedAlphaBeta(Convert.ToInt64(commandSyntax[2]), game.board,  game.NNUE, false);
                                    ReturnMove(move[0], int.MaxValue);
                                }
                                break;
                            case "perft":
                                try
                                {
                                    PerftOut(Convert.ToInt32(commandSyntax[2]), game.board);
                                }
                                catch
                                {
                                    Console.WriteLine("there was an Error!\n");
                                }
                                break;
                            case "depth":
                                try
                                {
                                    move[0] = alphaBetaSearch.IterativeDeepening(game.board, Convert.ToInt32(commandSyntax[2]), game.NNUE, false);
                                    ReturnMove(move[0], int.MaxValue);
                                }
                                catch
                                {
                                    Console.WriteLine("there was an Error!\n");
                                }
                                break;
                            case "nodes":
                                if (move != null)
                                    ReturnMove(move[0], move[1]);
                                break;
                            default:
                                if (game.USE_MCTS)
                                {
                                    if (move != null)
                                        ReturnMove(move[0], move[1]);
                                }
                                else
                                {
                                    move[0] = alphaBetaSearch.IterativeDeepening(game.board,  AlphaBeta.MAX_DEPTH, game.NNUE, false);
                                    ReturnMove(move[0], int.MaxValue);
                                }
                                break;
                        }
                    }
                    else
                    {
                        move[0] = alphaBetaSearch.IterativeDeepening(game.board, AlphaBeta.MAX_DEPTH, game.NNUE, false);
                        ReturnMove(move[0], int.MaxValue);   
                    }
                    break;
                case "setoption":
                    if (commandSyntax[1] == "name")
                    {
                        switch (commandSyntax[2])
                        {
                            case "EvalFile":
                                try
                                {
                                    game.NetName = commandSyntax[4];
                                }
                                catch
                                {
                                    Console.WriteLine("No such File: " + commandSyntax[4] + "\n");
                                }
                                break;
                            case "Use":
                                if (commandSyntax[3] == "NNUE")
                                {
                                    if (commandSyntax[5] == "true")
                                        game.NNUE = true;
                                    else if (commandSyntax[5] == "false")
                                        game.NNUE = false;
                                }
                                break;                      
                            case "Hash":
                                try
                                {
                                    game.HashSize = Convert.ToInt32(commandSyntax[4]);
                                    game.HashSize = Math.Max(Math.Min(game.HashSize, 10000), 1);
                                    alphaBetaSearch.tTable.Resize(game.HashSize);
                                }
                                catch { Console.WriteLine("{0} is not a number \n", commandSyntax[4]); }
                                break;
                        }
                    }
                    break;
                case "position":
                    switch (commandSyntax[1])
                    {
                        case "startpos":
                            ResetBoard();
                            try
                            {
                                if (commandSyntax[2] == "moves")
                                {
                                    string[] move_commands = new string[commandSyntax.Count - 3];

                                    for (int i = 3; i < commandSyntax.Count; i++)
                                        move_commands[i - 3] = commandSyntax[i];

                                    game.board = PlayGameFromCommand(move_commands, false);
                                }
                            }
                            catch
                            { }
                            break;
                        case "fen":
                            try
                            {
                                ResetBoard();
                                game.board = chess_stuff.LoadPositionFromFen(game.board, commandSyntax[2] + " " + commandSyntax[3] + " " + commandSyntax[4] + " " + commandSyntax[5]);
                                try
                                {
                                    for (int i = 5; i < commandSyntax.Count; i++)
                                    {
                                        if (commandSyntax[i] == "moves")
                                        {
                                            string[] MoveComands = new string[commandSyntax.Count - (i + 1)];

                                            for (int j = i + 1; j < commandSyntax.Count; j++)
                                                MoveComands[j - (i + 1)] = commandSyntax[j];

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
    public int[] PlanMoveTime(List<string> command, Position board, bool useNNUE)
    {
        int wtime = 0, btime = 0, winc = 0, binc = 0;
        int timeMe = 0, timeEnemy = 0, meInc = 0, enemyInc = 0;
        long timeToUse = 0;
        bool change_time = true;
        for (int i = 0; i < command.Count - 1; i++)
        {
            switch(command[i])
            {
                case "wtime":
                    wtime = Convert.ToInt32(command[i + 1]);
                    break;
                case "btime":
                    btime = Convert.ToInt32(command[i + 1]);
                    break;
                case "winc":
                    winc = Convert.ToInt32(command[i + 1]);
                    break;
                case "binc":
                    binc = Convert.ToInt32(command[i + 1]);
                    break;
                case "movestogo":
                    movestogo = Convert.ToInt32(command[i + 1]);
                    movelimit = true;
                    break;
            }
        }
        if(board.color == 0)
        {
            timeMe = btime;
            timeEnemy = wtime;
            meInc = binc;
            enemyInc = winc;
        }
        else
        {
            timeMe = wtime;
            timeEnemy = btime;
            meInc = winc;
            enemyInc = binc;
        }

        timeToUse = meInc;

        float moveNumber = Math.Min(played_moves, 10);
        played_moves++;

        if (movestogo <= 10 && !movelimit)
            movestogo += 15;

        timeToUse += (long)(timeMe / Math.Max(movestogo - 1, 1));
        movestogo--;

        return new int[] { alphaBetaSearch.TimedAlphaBeta(timeToUse - 50, board, useNNUE, change_time), 0 };
    }

    public void LoadPositionBoard()
    {
        movestogo = 40;
        played_moves = 0;
        alphaBetaSearch.timeToUse = 0;
        game.Playing = false;
        alphaBetaSearch.tTable.Resize(game.HashSize);
        game.board = chess_stuff.LoadPositionFromFen(game.board, Game.StartPosition);
    }

    public void ResetBoard()
    {
        alphaBetaSearch.timeToUse = 0;
        alphaBetaSearch.tTable.Resize(game.HashSize);
        game.board = chess_stuff.LoadPositionFromFen(game.board, Game.StartPosition);
    }

    public void PerftOut(int depth, Position board)
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();
        bool in_check = alphaBetaSearch.moveGenerator.check(board, false);
        ReverseMove move_undo = new ReverseMove();
        //instantiate the movelist
        int[][] movelist = new int[depth][];
        for (int i = 0; i < depth; i++)
            movelist[i] = new int[218];
        movelist[depth - 1] = alphaBetaSearch.moveGenerator.LegalMoveGenerator(board, in_check, move_undo, movelist[depth - 1]);
        int movelist_length = alphaBetaSearch.moveGenerator.moveIdx;
        long score = 0, counter = 0;
        for (int i = 0; i < movelist_length; i++)
        {
            board = alphaBetaSearch.moveGenerator.MakeMove(board, movelist[depth - 1][i], true, move_undo);
            if (!alphaBetaSearch.moveGenerator.illegal_position)
            {
                if (depth - 1 != 0)
                    score = perft(board, depth - 1, alphaBetaSearch.moveGenerator.FastCheck(board, movelist[depth - 1][i]), movelist);
                else
                    score = 1;

                if (score != 0)
                    Console.WriteLine("{0}: {1}", chess_stuff.move_to_string(movelist[depth - 1][i]), score);
                counter += score;
            }
            else
                alphaBetaSearch.moveGenerator.illegal_position = false;
            board = alphaBetaSearch.moveGenerator.unmake_move(board, move_undo);
        }

        Console.WriteLine("{0} nodes explored in {1} millisecondseconds", counter, sw.ElapsedMilliseconds);
        if (stop)
            stop = false;
    }

    public long perft(Position board, int depth, bool in_check, int[][] movelist)
    {
        if (stop)
            return 0;

        ReverseMove move_undo = new ReverseMove();
        movelist[depth - 1] = alphaBetaSearch.moveGenerator.LegalMoveGenerator(board, in_check, move_undo, movelist[depth - 1]);

        int movelist_length = alphaBetaSearch.moveGenerator.moveIdx;

        if (depth == 1)
            return movelist_length;

        long counter = 0;

        for (int i = 0; i < movelist_length; i++)
        {
            board = alphaBetaSearch.moveGenerator.MakeMove(board, movelist[depth - 1][i], true, move_undo);

            if (!alphaBetaSearch.moveGenerator.illegal_position)
                counter += perft(board, depth - 1, alphaBetaSearch.moveGenerator.FastCheck(board, movelist[depth - 1][i]), movelist);
            else
                alphaBetaSearch.moveGenerator.illegal_position = false;
            board = alphaBetaSearch.moveGenerator.unmake_move(board, move_undo);
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

    public void print_board_and_info(Position board)
    {
        chess_stuff.display_board(board);
        Console.WriteLine();
        Console.WriteLine("Fen: {0}", chess_stuff.generate_fen_from_position(board));
        Console.WriteLine("Key: {0}", alphaBetaSearch.hash.HashPosition(board));
        Console.WriteLine();
    }
    public List<string> SyntaxWithoutHoles(string[] syntax)
    {
        List<string> output = new List<string>();

        foreach (string word in syntax)
        {
            if (!string.IsNullOrEmpty(word))   
                output.Add(word);
        }
        return output;
    }

    public Position PlayGameFromCommand(string[] Commands , bool TreeUpdate)
    {
        int[] PlayingMoves = new int[Commands.Length];

        for (int i = 0; i < Commands.Length; i++)
        {
           PlayingMoves[i] = chess_stuff.convert_string_to_move(Commands[i]);
        }

        return alphaBetaSearch.PlayGameFromMoves(game.board, PlayingMoves);
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

    public void ReturnEvaluation(Position board)
    {
        if (board.color == 0)
            Console.WriteLine("\nNNUE network contributions (Black to move)\n");
        else
            Console.WriteLine("\nNNUE network contributions (White to move)\n");
        float Value = 0;

        alphaBetaSearch.valueNet.set_acc_from_position(board);
        Value = ((float)alphaBetaSearch.valueNet.AccToOutput(alphaBetaSearch.valueNet.acc, (byte)(board.color)) / 1000);

        Console.WriteLine("NNUE evaluation                  {0} (white side)", ReturnNumber((float)Value));

        Value = (float)alphaBetaSearch.eval.PestoEval(board) / 1000;

        Console.WriteLine("Classical evaluation             {0} (white side)\n", ReturnNumber((float)Value));
    }
}
