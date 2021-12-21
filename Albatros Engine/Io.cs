using System;
using System.Diagnostics;
using System.Collections.Generic;
class Io
{
    string CurrentCommand = "";
    Game game = new Game();
    Treesearch treesearch = new Treesearch(1245845, true, 5);
    public string[] LastPositionCommand;
    int fd = 0;
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
                    treesearch.Test(Convert.ToInt32(command_syntax[1]) , game.Board , game.Board2);
                    break;
                case "d":
                    DisplayCurrentBoard(game.Board);
                    break;
                case "uci":
                    Console.WriteLine(
                        "id name Albatros v.1" +
                        "\nid author D.Grévent" +
                        "\n" +
                        "\noption name Ponder type check default false" +
                        "\noption name EvalFile type string default ValueNet-0.nn" +
                        "\noption name LearningBufferSize type spin default 100000 min 1 max 10000000" +
                        "\noption name LearningNodeCount type spin default 100 min 1 max 10000" +
                        "\noption name LearningGameLength type spin default 1000 min 1 max 10000" +
                        "\noption name LearningCoefficient type spin default 0.01 min 0 max 1" +
                        "\noption name LearningInertia type spin default 0.5 min 0 max 1" +
                        "\noption name Learning type check default false" +
                        "\noption name NNUE type check default true" +
                        "\noption name Threads type spin default 5 min 1"+
                        "\nuciok");
                    break;
                case "ucinewgame":
                    LoadPositionBoard();
                    break;
                case "eval":
                    ReturnEvaluation((byte)game.Turn, game.Board);
                    break;
                case "go":
                    switch (command_syntax[1])
                    {
                        case "perft":
                            try
                            {
                                PrintMoovesFromPosition(game.Board, (byte)game.Turn, Convert.ToInt32(command_syntax[2]));
                            }
                            catch
                            {
                                Console.WriteLine("there was an Error!\n");
                            }
                            break;
                        case "depth":
                            try
                            {
                                Stopwatch stw = new Stopwatch();
                                stw.Start();
                                int[] Move = treesearch.MinMaxAlphaBeta(game.Board, (byte)game.Turn, Convert.ToInt32(command_syntax[2]), game.NNUE);
                                ReturnMoove(Move, new int[0]);
                            }
                            catch
                            {
                                Console.WriteLine("there was an Error!\n");
                            }
                            break;
                        case "nodes":
                            Stopwatch sw = new Stopwatch();
                            sw.Start();
                            int[][] Moove = treesearch.MultithreadMcts(game.Board, (byte)game.Turn, Convert.ToInt32(command_syntax[2]) , game.NNUE , game.TreadCount);
                            if (Moove != null)
                                ReturnMoove(Moove[0], Moove[1]);
                            break;
                        default:
                            Console.WriteLine("Unknown command: " + command + "\n");
                            break;
                    }
                    break;
                case "setoption":
                    if (command_syntax[1] == "name" && command_syntax[3] == "value")
                    {
                        switch (command_syntax[2])
                        {
                            case "Ponder":
                                if (command_syntax[4] == "true")
                                    fd = 0;
                                else if (command_syntax[4] == "false")
                                    fd = 1;
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
                            case "Elo":
                                try
                                {
                                    game.Elo = Convert.ToInt32(command_syntax[4]);
                                    if (game.Elo < 0)
                                        game.Elo = 0;
                                }
                                catch
                                {
                                    Console.WriteLine("{0} is not a number \n" , command_syntax[4]);
                                }
                                break;
                            case "NNUE":
                                if (command_syntax[4] == "true")
                                    game.NNUE = true;
                                else if (command_syntax[4] == "false")
                                    game.NNUE = false;
                                break;
                            case "Threads":
                                try
                                {
                                    game.TreadCount = Convert.ToInt32(command_syntax[4]);
                                    if (game.TreadCount < 1)
                                        game.TreadCount = 1;
                                    treesearch.ChangeThreadCount(game.TreadCount);
                                }
                                catch { }
                                break;
                        }
                    }
                    break;
                case "position":
                    switch (command_syntax[1])
                    {
                        case "startpos":
                            if (!game.Playing)
                            {
                                game.Playing = true;
                                game.Board = game.LoadPositionFromFen(game.StartPosition);
                                try
                                {
                                    if (command_syntax[2] == "moves")
                                    {
                                        string[] MoovesComands = new string[command_syntax.Length - 3];

                                        for (int i = 3; i < command_syntax.Length; i++)
                                            MoovesComands[i - 3] = command_syntax[i];

                                        byte[][,] array = PlayGameFromCommand(MoovesComands , false);
                                        game.Turn = array[1][0, 0];
                                        Array.Copy(array[0], game.Board, game.Board.Length);
                                    }
                                }
                                catch
                                { }
                            }
                            else
                            {
                                try
                                {
                                    string[] MoovesComands = new string[2];

                                    for (int i = command_syntax.Length - 2; i < command_syntax.Length; i++)
                                        MoovesComands[i + 2 - command_syntax.Length] = command_syntax[i];

                                    byte[][,] array = PlayGameFromCommand(MoovesComands , true);
                                    game.Turn = array[1][0, 0];
                                    Array.Copy(array[0], game.Board, game.Board.Length);
                                }
                                catch
                                { }
                            }
                            break;
                        case "fen":
                            try
                            {
                                if (!game.Playing)
                                {
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

                                                byte[][,] array = PlayGameFromCommand(MoovesComands , false);
                                                game.Turn = array[1][0, 0];
                                                Array.Copy(array[0], game.Board, game.Board.Length);
                                            }
                                        }

                                    }
                                    catch
                                    { }
                                }
                                else
                                {
                                    try
                                    {
                                        string[] MoovesComands = new string[2];

                                        for (int i = command_syntax.Length - 2; i < command_syntax.Length; i++)
                                            MoovesComands[i + 2 - command_syntax.Length] = command_syntax[i];

                                        byte[][,] array = PlayGameFromCommand(MoovesComands , true);
                                        game.Turn = array[1][0, 0];
                                        Array.Copy(array[0], game.Board, game.Board.Length);
                                    }
                                    catch
                                    { }
                                }
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
    public void LoadPositionBoard()
    {
        game.Playing = false;
        treesearch.CurrentTree = null;
        game.Board = game.LoadPositionFromFen(game.StartPosition);
      //  game.Board2 = game.LoadPositionFromFen(game.TestPosition);
    }
    public void PrintMoovesFromPosition(byte[,] InputBoard, byte color, int depthPly)
    {
        byte othercolor = 0;
        if (color == 0)
            othercolor = 1;
        int completCount = 0;
        int currentNumber = 0;
        List<int[]> Mooves = treesearch.ReturnPossibleMoves(InputBoard, color);
        string[] ConvertNumToLetter = new string[] { "0", "a", "b", "c", "d", "e", "f", "g", "h" };
        string[] Promotion = new string[] { "", "n", "b", "q", "r" };
        Stopwatch watch = new Stopwatch();
        watch.Start();
        //if not Checkmate
        if (treesearch.PossiblePositionCounter(InputBoard, 1, color) != 0)
        {
            foreach (int[] Move in Mooves)
            {
                if (Move.Length != 5 || !treesearch.CastlingCheck(InputBoard, Move))
                {
                    InputBoard = treesearch.PlayMove(InputBoard, color, Move);
                    int[] MoveUndo = new int[treesearch.UnmakeMove.Length];
                    Array.Copy(treesearch.UnmakeMove, MoveUndo, treesearch.UnmakeMove.Length);
                    currentNumber = treesearch.PossiblePositionCounter(InputBoard, depthPly - 1, othercolor);
                    InputBoard = treesearch.UndoMove(InputBoard, MoveUndo);
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
    public void ReturnMoove(int[] Moove, int[] PonderMoove)
    {
        string[] ConvertNumToLetter = new string[] { "0", "a", "b", "c", "d", "e", "f", "g", "h" };
        string[] Promotion = new string[] { "", "n", "b", "q", "r" };
        string Output = "";
        //Promoting Pawn
        if (Moove.Length == 5)
            Output = "bestmove " + ConvertNumToLetter[Moove[0]] + Moove[1] + ConvertNumToLetter[Moove[2]] + Moove[3] + Promotion[Moove[4]];

        //Normal Piece
        else
            Output = "bestmove " + ConvertNumToLetter[Moove[0]] + Moove[1] + ConvertNumToLetter[Moove[2]] + Moove[3];

        if (PonderMoove.Length != 0)
        {
            //Promoting Pawn
            if (PonderMoove.Length == 5)
                Output += " ponder " + ConvertNumToLetter[PonderMoove[0]] + PonderMoove[1] + ConvertNumToLetter[PonderMoove[2]] + PonderMoove[3] + Promotion[PonderMoove[4]];

            //Normal Piece
            else
                Output += " ponder " + ConvertNumToLetter[PonderMoove[0]] + PonderMoove[1] + ConvertNumToLetter[PonderMoove[2]] + PonderMoove[3];
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
        char[] MooveInParts;
        for (int i = 0; i < Commands.Length; i++)
        {
            MooveInParts = Commands[i].ToCharArray();
            if (MooveInParts.Length == 4)
            {
                PlayingMooves[i] = new int[4] { GetPlaceInArray(ConvertNumToLetter, MooveInParts[0]), Convert.ToInt32(MooveInParts[1]) - 48, GetPlaceInArray(ConvertNumToLetter, MooveInParts[2]), Convert.ToInt32(MooveInParts[3]) - 48 };
            }
            else if (MooveInParts.Length == 5)
            {
                PlayingMooves[i] = new int[5] { GetPlaceInArray(ConvertNumToLetter, MooveInParts[0]), Convert.ToInt32(MooveInParts[1]) - 48, GetPlaceInArray(ConvertNumToLetter, MooveInParts[2]), Convert.ToInt32(MooveInParts[3]) - 48, GetPlaceInArray(Promotion, MooveInParts[4]) };
            }
        }
        return treesearch.PlayGameFromMooves(game.Board, (byte)game.Turn, PlayingMooves , TreeUpdate);
    }
    public void PrintEval(byte color, byte[,] Position)
    {
        string Pointer = "";
        string ShowPointer = " <-- this bucket is used";
        float[,]Table = treesearch.ValueNet.ReturnNetOutputs(treesearch.ValueNet.BoardToHalfKav2(Position, color));
        int Place = treesearch.ValueNet.GetPlace_0_To_7_();
        Console.WriteLine("+------------+------------+------------+------------+\n" +
            "|   Bucket   |  Material  | Positional |   Total    |\n" +
            "|            |   (PSQT)   |  (Layers)  |            |\n" +
            "+------------+------------+------------+------------+");
        string SignA, SignB, SignC;
        for (int i = 0; i < 8; i++)
        {
            float Sum = Table[0, i] + Table[1, i];
            if (Math.Round(Table[0, i], 2)>= -0)
                SignA = "+";
            else
                SignA = "-";
            Table[0, i] = Math.Abs(Table[0, i]);
            if (Math.Round(Table[1, i], 2) >= -0)
                SignB = "+";
            else
                SignB = "-";
            Table[1, i] = Math.Abs(Table[1, i]);
            if (Math.Round(Sum, 2) >= -0)
                SignC = "+";
            else
                SignC = "-";

            Sum = Math.Abs(Sum);
            if (i == Place)
                Pointer = ShowPointer;
            else
                Pointer = "";
            Console.WriteLine("|  {0}         |  {1}  {2}   |  {3}  {4}   |  {5}  {6}   |{7}", i, SignA, string.Format("{0:N2}", Math.Round(Table[0, i], 2)), SignB, string.Format("{0:N2}", Math.Round(Table[1, i], 2)), SignC, string.Format("{0:N2}", Math.Round(Sum, 2)), Pointer);
        }
        Console.WriteLine("+------------+------------+------------+------------+\n\n");
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
        if (Output.ToCharArray().Length != 4)
            Output += 0;
        return Sign + Output;
    }
    public void PrintValuePerPiece(byte color, byte[,] Position)
    {
        PSQTValue[,] BoardValues = treesearch.ValueNet.PSQTOUtput(Position, color);
        string LineA = "|";
        string LineB = "|";
        Console.WriteLine("NNUE derived piece values:");
        Console.WriteLine("+-------+-------+-------+-------+-------+-------+-------+-------+");
        for (int i = 8; i > 0; i--)
        {
            LineA = "|";
            LineB = "|";
            for (int j = 1; j < 9; j++)
            {
                if (BoardValues[j, i] != null)
                {
                    LineA += "   " + BoardValues[j, i].Name + "   |";
                    if (!float.IsNaN(BoardValues[j, i].Value))
                        LineB += " " + ReturnNumber(BoardValues[j, i].Value) + " |";
                    else
                        LineB += "       |";
                }
                else
                {
                    LineA += "       |";
                    LineB += "       |";
                }
            }
            Console.WriteLine(LineA);
            Console.WriteLine(LineB);
            Console.WriteLine("+-------+-------+-------+-------+-------+-------+-------+-------+");
        }
        Console.WriteLine("\n\n");
    }
    public void ReturnEvaluation(byte color, byte[,] Position)
    {
        PrintValuePerPiece(color, Position);
        if (color == 0)
            Console.WriteLine("\nNNUE network contributions (Black to move)");
        else
            Console.WriteLine("\nNNUE network contributions (White to move)");
        PrintEval(color, Position);

        double Value = treesearch.ValueNet.UseNet(Position, color);
        if (color == 1)
            Value = -Value;
        Console.WriteLine("NNUE evaluation          {0} (white side)",ReturnNumber((float)Value));

        Value = treesearch.eval.PestoEval(Position, color);
        if (color == 1)
            Value = -Value;
        Console.WriteLine("Classical evaluation     {0} (white side)\n", ReturnNumber((float)Value));
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
        game.training = new Training(game.Board, game.NetName, game.BufferSize, game.GameLength, game.NodeCount, game.Coefficient, game.TreadCount, game.Momentum, game.PlayingPosCount, game.TrainingSampleSize, game.NetDecay, game.Elo, game.Lambda);
    }
}
