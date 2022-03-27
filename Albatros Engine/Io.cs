using System;
using System.Collections.Generic;
using System.Diagnostics;
class Io
{
    string CurrentCommand = "";
    Game game = new Game();
    public int[][] Move = new int[2][];
    Treesearch treesearch = new Treesearch(1245845, true, 5);
    Stopwatch sw = new Stopwatch();
    public AlphaBeta AlphaBetaSearch;
    int movestogo = 50;
    bool movelimit = false;
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
                    DisplayCurrentBoard(game.Board);
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
                    ReturnEvaluation((byte)game.Turn, game.Board);
                    break;
                case "go":
                    if(command_syntax.Length >= 2)
                    {
                        switch (command_syntax[1])
                        {
                            case "winc":
                                Move = PlanMoveTime(command_syntax, game.Board, (byte)game.Turn, game.NNUE, game.USE_MCTS);
                                if (Move != null)
                                    ReturnMove(Move[0], Move[1]);
                                break;
                            case "binc":
                                Move = PlanMoveTime(command_syntax, game.Board, (byte)game.Turn, game.NNUE, game.USE_MCTS);
                                if (Move != null)
                                    ReturnMove(Move[0], Move[1]);
                                break;
                            case "btime":
                                Move = PlanMoveTime(command_syntax, game.Board, (byte)game.Turn, game.NNUE, game.USE_MCTS);
                                if (Move != null)
                                    ReturnMove(Move[0], Move[1]);
                                break;
                            case "wtime":
                                Move = PlanMoveTime(command_syntax, game.Board, (byte)game.Turn, game.NNUE, game.USE_MCTS);
                                if (Move != null)
                                    ReturnMove(Move[0], Move[1]);
                                break;
                            case "movestogo":
                                Move = PlanMoveTime(command_syntax, game.Board, (byte)game.Turn, game.NNUE, game.USE_MCTS);
                                if (Move != null)
                                    ReturnMove(Move[0], Move[1]);
                                break;
                            case "ponder":
                                if (game.USE_MCTS)
                                {
                                    Move = treesearch.MultithreadMcts(game.Board, (byte)game.Turn, Int32.MaxValue, game.NNUE, game.ThreadCount, true, false, 0, game.c_puct);
                                    if (Move != null)
                                        ReturnMove(Move[0], Move[1]);
                                }
                                else
                                {
                                    Move[0] = AlphaBetaSearch.iterative_deepening(game.Board, (byte)game.Turn, byte.MaxValue, game.NNUE);
                                    ReturnMove(Move[0], new int[0]);
                                }
                                break;
                            case "infinite":
                                if (game.USE_MCTS)
                                {
                                    Move = treesearch.MultithreadMcts(game.Board, (byte)game.Turn, Int32.MaxValue, game.NNUE, game.ThreadCount, true, false, 0, game.c_puct);
                                    if (Move != null)
                                        ReturnMove(Move[0], Move[1]);
                                }
                                else
                                {
                                    Move[0] = AlphaBetaSearch.iterative_deepening(game.Board, (byte)game.Turn, byte.MaxValue, game.NNUE);
                                    ReturnMove(Move[0], new int[0]);
                                }
                                break;
                            case "movetime":
                                if (game.USE_MCTS)
                                {
                                    Move = treesearch.MultithreadMcts(game.Board, (byte)game.Turn, Int32.MaxValue, game.NNUE, game.ThreadCount, true, true, Convert.ToInt64(command_syntax[2]), game.c_puct);
                                    if (Move != null)
                                        ReturnMove(Move[0], Move[1]);
                                }
                                else
                                {
                                    Move[0] = AlphaBetaSearch.TimedAlphaBeta(Convert.ToInt64(command_syntax[2]), game.Board, (byte)game.Turn, game.NNUE);
                                    ReturnMove(Move[0], new int[0]);
                                }
                                break;
                            case "perft":
                                try
                                {
                                    PrintMovesFromPosition(game.Board, (byte)game.Turn, Convert.ToInt32(command_syntax[2]));
                                }
                                catch
                                {
                                    Console.WriteLine("there was an Error!\n");
                                }
                                break;
                            case "captures":
                                PrintCapturesFromPosition(game.Board, (byte)game.Turn);
                                break;
                            case "depth":
                                try
                                {
                                    Move[0] = AlphaBetaSearch.iterative_deepening(game.Board, (byte)game.Turn, Convert.ToInt32(command_syntax[2]), game.NNUE);
                                    ReturnMove(Move[0], new int[0]);
                                }
                                catch
                                {
                                    Console.WriteLine("there was an Error!\n");
                                }
                                break;
                            case "nodes":
                                Move = treesearch.MultithreadMcts(game.Board, (byte)game.Turn, Convert.ToInt32(command_syntax[2]), game.NNUE, game.ThreadCount, false, false, 0, game.c_puct);
                                if (Move != null)
                                    ReturnMove(Move[0], Move[1]);
                                break;
                            default:
                                if (game.USE_MCTS)
                                {
                                    Move = treesearch.MultithreadMcts(game.Board, (byte)game.Turn, Int32.MaxValue, game.NNUE, game.ThreadCount, true, false, 0, game.c_puct);
                                    if (Move != null)
                                        ReturnMove(Move[0], Move[1]);
                                }
                                else
                                {
                                    Move[0] = AlphaBetaSearch.iterative_deepening(game.Board, (byte)game.Turn, byte.MaxValue, game.NNUE);
                                    ReturnMove(Move[0], new int[0]);
                                }
                                break;
                        }
                    }
                    else
                    {
                        if (game.USE_MCTS)
                        {
                            Move = treesearch.MultithreadMcts(game.Board, (byte)game.Turn, Int32.MaxValue, game.NNUE, game.ThreadCount, true, false, 0, game.c_puct);
                            if (Move != null)
                                ReturnMove(Move[0], Move[1]);
                        }
                        else
                        {
                            Move[0] = AlphaBetaSearch.iterative_deepening(game.Board, (byte)game.Turn, byte.MaxValue, game.NNUE);
                            ReturnMove(Move[0], new int[0]);
                        }
                    }
                    break;
                case "export_net":
                    treesearch.ValueNet.SaveNet(command_syntax[1], false);
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
                                    treesearch.ChangeThreadCount(game.ThreadCount);
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

                                    byte[][,] array = PlayGameFromCommand(move_commands, false);
                                    game.Turn = array[1][0, 0];
                                    Array.Copy(array[0], game.Board, game.Board.Length);
                                }
                            }
                            catch
                            { }
                            break;
                        case "fen":
                            try
                            {
                                reset_board();
                                game.Board = game.LoadPositionFromFen(command_syntax[2] + " " + command_syntax[3] + " " + command_syntax[4] + " " + command_syntax[5]);
                                try
                                {
                                    for (int i = 5; i < command_syntax.Length; i++)
                                    {
                                        if (command_syntax[i] == "moves")
                                        {
                                            string[] MoovesComands = new string[command_syntax.Length - (i + 1)];

                                            for (int j = i + 1; j < command_syntax.Length; j++)
                                                MoovesComands[j - (i + 1)] = command_syntax[j];

                                            byte[][,] array = PlayGameFromCommand(MoovesComands, false);
                                            game.Turn = array[1][0, 0];
                                            Array.Copy(array[0], game.Board, game.Board.Length);
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
    public int[][] PlanMoveTime(string[] Command, byte[,] InputBoard, byte color , bool NNUE , bool USE_MCTS)
    {
        int wtime = 0, btime = 0, winc = 0, binc = 0;
        int timeMe = 0, TimeEnemy = 0, Meinc = 0, Enemyinc = 0;
        long timeToUse = 0;
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
        if(color == 0)
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

        if (Meinc != 0)
            timeToUse = Meinc;

        if (!movelimit)
        {
            if (movestogo >= 5)
            {
                timeToUse += timeMe / movestogo;
                movestogo--;
            }
            else
            {
                timeToUse = (9 * Meinc) / 10;
            }

            if (timeToUse >= Meinc * 2)
                movestogo++;

        }
        else
            timeToUse += timeMe / (movestogo + 1);

        if (USE_MCTS)
        {
            if (timeToUse < 3000)
                return new int[][] { AlphaBetaSearch.TimedAlphaBeta(timeToUse - 50, InputBoard, color, NNUE), new int[0] };
            else
                return treesearch.MultithreadMcts(game.Board, (byte)game.Turn, Int32.MaxValue, game.NNUE, game.ThreadCount, true, true, timeToUse - 500, game.c_puct);
        }
        else
            return new int[][] { AlphaBetaSearch.TimedAlphaBeta(timeToUse - 20, InputBoard, color, NNUE), new int[0] };
    }
    public void LoadPositionBoard()
    {
        AlphaBetaSearch.time_to_use = 0;
        movestogo = 50;
        game.Playing = false;
        treesearch.CurrentTree = null;
        AlphaBetaSearch.HashTable = new byte[game.HashSize * 55556, 18];
        AlphaBetaSearch.MoveGenerator.fifty_move_rule = 0;
        game.Board = game.LoadPositionFromFen(game.StartPosition);
    }
    public void reset_board()
    {
        AlphaBetaSearch.time_to_use = 0;
        AlphaBetaSearch.HashTable = new byte[game.HashSize * 55556, 18];
        AlphaBetaSearch.MoveGenerator.fifty_move_rule = 0;
        game.Board = game.LoadPositionFromFen(game.StartPosition);
    }
    public void PrintMovesFromPosition(byte[,] InputBoard, byte color, int depthPly)
    {
        byte othercolor = 0;
        if (color == 0)
            othercolor = 1;
        int completCount = 0;
        int currentNumber = 0;
        List<int[]> Moves = treesearch.MoveGenerator.ReturnPossibleMoves(InputBoard, color);
        string[] ConvertNumToLetter = new string[] { "0", "a", "b", "c", "d", "e", "f", "g", "h" };
        string[] Promotion = new string[] { "", "n", "b", "q", "r" };
        Stopwatch watch = new Stopwatch();
        watch.Start();
        //if not Checkmate
        if (treesearch.PossiblePositionCounter(InputBoard, 1, color) != 0)
        {
            foreach (int[] Move in Moves)
            {
                if (Move.Length != 5 || !treesearch.MoveGenerator.CastlingCheck(InputBoard, Move))
                {
                    InputBoard = treesearch.MoveGenerator.PlayMove(InputBoard, color, Move);
                    int[] MoveUndo = new int[treesearch.MoveGenerator.UnmakeMove.Length];
                    Array.Copy(treesearch.MoveGenerator.UnmakeMove, MoveUndo, treesearch.MoveGenerator.UnmakeMove.Length);
                    currentNumber = treesearch.PossiblePositionCounter(InputBoard, depthPly - 1, othercolor);
                    InputBoard = treesearch.MoveGenerator.UndoMove(InputBoard, MoveUndo);
                    if (currentNumber != 0 || depthPly - 1 > 0)
                    {
                        //Promoting Pawn
                        if (Move.Length == 5)
                        {
                            Console.WriteLine(ConvertNumToLetter[Move[0]] + Move[1] + ConvertNumToLetter[Move[2]] + Move[3] + Promotion[Move[4]] + ": " + currentNumber);
                            completCount += currentNumber;
                        }
                        //Normal Piece
                        else
                        {
                            Console.WriteLine(ConvertNumToLetter[Move[0]] + Move[1] + ConvertNumToLetter[Move[2]] + Move[3] + ": " + currentNumber);
                            completCount += currentNumber;
                        }
                    }
                }
            }
        }
        Console.WriteLine("\nNodes searched: {0}  \nElapsed Time: {1}\n", completCount, watch.ElapsedMilliseconds);
    }
    public void PrintCapturesFromPosition(byte[,] InputBoard, byte color)
    {
        byte othercolor = (byte)(1 - color);
        int completCount = 0;
        int currentNumber = 1;
        List<int[]> Moves = treesearch.MoveGenerator.ReturnPossibleCaptures(InputBoard, color);
        string[] ConvertNumToLetter = new string[] { "0", "a", "b", "c", "d", "e", "f", "g", "h" };
        string[] Promotion = new string[] { "", "n", "b", "q", "r" };
        Stopwatch watch = new Stopwatch();
        watch.Start();
        //if not Checkmate
        if (treesearch.PossiblePositionCounter(InputBoard, 1, color) != 0)
        {
            foreach (int[] Move in Moves)
            {
                if (Move.Length != 5 || !treesearch.MoveGenerator.CastlingCheck(InputBoard, Move))
                {
                    InputBoard = AlphaBetaSearch.MoveGenerator.PlayMove(InputBoard, color, Move);
                    if (currentNumber != 0 && !AlphaBetaSearch.MoveGenerator.CompleteCheck(InputBoard , othercolor))
                    {
                        //Promoting Pawn
                        if (Move.Length == 5)
                        {
                            Console.WriteLine(ConvertNumToLetter[Move[0]] + Move[1] + ConvertNumToLetter[Move[2]] + Move[3] + Promotion[Move[4]] + ": " + currentNumber);
                            completCount += currentNumber;
                        }
                        //Normal Piece
                        else
                        {
                            Console.WriteLine(ConvertNumToLetter[Move[0]] + Move[1] + ConvertNumToLetter[Move[2]] + Move[3] + ": " + currentNumber);
                            completCount += currentNumber;
                        }
                    }
                    InputBoard = AlphaBetaSearch.MoveGenerator.UndoMove(InputBoard, AlphaBetaSearch.MoveGenerator.UnmakeMove);
                }
            }
        }
        Console.WriteLine("\nNodes searched: {0}  \nElapsed Time: {1}\n", completCount, watch.ElapsedMilliseconds);
    }
    public void ReturnMove(int[] Move, int[] PonderMove)
    {
        string[] ConvertNumToLetter = new string[] { "0", "a", "b", "c", "d", "e", "f", "g", "h" };
        string[] Promotion = new string[] { "", "n", "b", "q", "r" };
        string Output = "";

        //Promoting Pawn
        if (Move.Length == 5)
            Output = "bestmove " + ConvertNumToLetter[Move[0]] + Move[1] + ConvertNumToLetter[Move[2]] + Move[3] + Promotion[Move[4]];

        //Normal Piece
        else
            Output = "bestmove " + ConvertNumToLetter[Move[0]] + Move[1] + ConvertNumToLetter[Move[2]] + Move[3];

        if (PonderMove.Length >= 4)
        {
            //Promoting Pawn
            if (PonderMove.Length == 5)
                Output += " ponder " + ConvertNumToLetter[PonderMove[0]] + PonderMove[1] + ConvertNumToLetter[PonderMove[2]] + PonderMove[3] + Promotion[PonderMove[4]];

            //Normal Piece
            else
                Output += " ponder " + ConvertNumToLetter[PonderMove[0]] + PonderMove[1] + ConvertNumToLetter[PonderMove[2]] + PonderMove[3];
        }

        Console.WriteLine(Output);
    }
    public void DisplayCurrentBoard(byte[,] InputBoard)
    {
        string spacer = "+---+---+---+---+---+---+---+---+";
        string backrow = "  a   b   c   d   e   f   g   h";
        string[] rows = new string[8];
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
    public byte[][,] PlayGameFromCommand(string[] Commands , bool TreeUpdate)
    {
        char[] ConvertNumToLetter = new char[] { '0', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h' };
        char[] Promotion = new char[] { '0', 'n', 'b', 'q', 'r' };
        int[][] PlayingMooves = new int[Commands.Length][];
        char[] MoveInParts;
        for (int i = 0; i < Commands.Length; i++)
        {
            MoveInParts = Commands[i].ToCharArray();
            if (MoveInParts.Length == 4)
            {
                PlayingMooves[i] = new int[4] { GetPlaceInArray(ConvertNumToLetter, MoveInParts[0]), Convert.ToInt32(MoveInParts[1]) - 48, GetPlaceInArray(ConvertNumToLetter, MoveInParts[2]), Convert.ToInt32(MoveInParts[3]) - 48 };
            }
            else if (MoveInParts.Length == 5)
            {
                PlayingMooves[i] = new int[5] { GetPlaceInArray(ConvertNumToLetter, MoveInParts[0]), Convert.ToInt32(MoveInParts[1]) - 48, GetPlaceInArray(ConvertNumToLetter, MoveInParts[2]), Convert.ToInt32(MoveInParts[3]) - 48, GetPlaceInArray(Promotion, MoveInParts[4]) };
            }
        }
        return AlphaBetaSearch.PlayGameFromMoves(game.Board, (byte)game.Turn, PlayingMooves , TreeUpdate);
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
    public void ReturnEvaluation(byte color, byte[,] Position)
    {
        if (color == 0)
            Console.WriteLine("\nNNUE network contributions (Black to move)\n");
        else
            Console.WriteLine("\nNNUE network contributions (White to move)\n");
        float Value = 0;

        Value = (float)treesearch.ValueNet.UseNet(Position, color);
        if (color == 1)
            Value = -Value;
        Console.WriteLine("NNUE evaluation                  {0} (white side)", ReturnNumber((float)Value));

        int[] KingSquares = treesearch.MoveGenerator.FindKings(Position);
        AlphaBetaSearch.ValueNet.set_acc_from_position(Position);
        Value = AlphaBetaSearch.ValueNet.AccToOutput(AlphaBetaSearch.ValueNet.acc, color);
        if (color == 1)
            Value = -Value;
        Console.WriteLine("NNUE Avx2 evaluation             {0} (white side)", ReturnNumber((float)Value));

        Value = treesearch.eval.PestoEval(Position, color);
        if (color == 1)
            Value = -Value;
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
        game.training = new Training(game.Board, game.NetName, game.BufferSize, game.GameLength, game.NodeCount, game.learning_rate, game.ThreadCount, game.Momentum, game.batch_size, game.Lambda, game.Play, game.LogFile, game.depthPly);
    }
}
