using System;
using System.Threading;
using System.Collections.Generic;


class Game
{
    public string StartPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    public byte[,] Board;
    public static bool quit = false;
    public bool Playing = false;
    public Training training;
    public string CurrentFen;
    static Thread CommandExecute;
    public int Turn = 1;
    static Io io = new Io();
    static string output = "";
    static string CommandBuffer = "";
    //Training Parameters
    public int Elo = 100;
    public float Lambda = 1;
    public int BufferSize = 1000000;
    public int TrainingSampleSize = 10000;
    public int GameLength = 350;
    public string NetName = "ValueNet.nnue";
    public int NodeCount = 100;
    public float Coefficient = 1;
    public float Momentum = 0.9f;
    public float NetDecay = 0.75f;

    //Other Parameters
    public float c_puct = 10;
    public bool IsPlaying = false;
    public bool NNUE = false;
    public bool HalfKav2 = true;
    public bool HalfKp = false;
    public int ThreadCount = 5;
    static void Main(string[] args)
    {
        Console.WriteLine("Albatros");
        Init();
        while (!quit)
        { 
            Update();
        }
    }
    static void Init()
    {
        //Loads Start Position Into Current Position Board
        io.LoadPositionBoard();
        CommandExecute = new Thread(io.ThreadStart);
    }
    public void SetOutput(string Input)
    {
        output = Input;
    }
    static void Update()
    {
        if (CommandBuffer != "")
        {
            if (!CommandExecute.IsAlive)
            {
                CommandExecute = new Thread(io.ThreadStart);
                io.SetCurrentCommand(CommandBuffer);
                CommandBuffer = "";
                CommandExecute.Start();
            }
        }
        else
        {
            string Command = Console.ReadLine();
            string[] Input = io.SyntaxWithoutHoles(Command.Split(' '));
            if (Input.Length != 0 && Input[0] == "stop")
            {
                io.Stop();
            }
            else if (Input.Length != 0 && Input[0] == "quit")
            {
                io.Stop();
                quit = true;
            }
            else if (Input.Length != 0 && Input[0] == "Training")
            {
                if (Input.Length > 2)
                {
                    switch (Input[2])
                    {
                        case "-Elo":
                            try
                            {
                                io.SetElo(Convert.ToInt32(Input[3]));
                            }
                            catch
                            {
                            }
                            break;
                    }
                }
                CommandExecute = new Thread(io.ThreadStart);
                io.TrainingStart();
            }
            else if (!CommandExecute.IsAlive)
            {
                CommandExecute = new Thread(io.ThreadStart);
                io.SetCurrentCommand(Command);
                CommandExecute.Start();
            }
            else if (CommandExecute.IsAlive)
            {
                CommandBuffer = Command;
            }
        }
    }

    public byte[,] LoadPositionFromFen(string Fen)
    {
        int CurrentDisplacement = 0;
        int displacementCounter = 0;
        char[] casteling;
        bool QB = false, KB = false, QW = false, KW = false;
        int[] EnPassentCoordinate = new int[2];
        bool EnPassent = false;
        char[] EnPassentPawn;
        byte[,] Output = new byte[9, 9];
        //First cut the fen into its different parts
        string[] Semantics = Fen.Split(' ');
        //Then cut the Board into files
        string[] Files = Semantics[0].Split('/');
        //Find the coordinates of the enPassent Piece
        if (Semantics[3] != "-")
        {
            EnPassent = true;
            EnPassentPawn = Semantics[3].ToCharArray();
            char[] Numbers = new char[] { '1', '2', '3', '4', '5', '6', '7', '8' };
            char[] Letters = new char[] { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h' };
            for (int i = 0; i < 8; i++)
            {
                if (Numbers[i] == EnPassentPawn[1])
                {
                    if (i == 3)
                        EnPassentCoordinate[0] = i + 1;
                    else
                        EnPassentCoordinate[0] = i - 1;
                }
                if (Letters[i] == EnPassentPawn[0])
                    EnPassentCoordinate[1] = i;
            }
        }
        //Look For Castelling
        if (Semantics[2] != "-")
        {
            casteling = Semantics[2].ToCharArray();
            for (int i = 0; i < casteling.Length; i++)
            {
                if (casteling[i] == 'K')
                    KW = true;
                else if (casteling[i] == 'Q')
                    QW = true;
                else if (casteling[i] == 'k')
                    KB = true;
                else if (casteling[i] == 'q')
                    QB = true;
            }
        }
        //Go trough all the ranks and divide them into pieces
        char[] Pieces;
        for (int i = 0; i < 8; i++)
        {
            Pieces = Files[7 - i].ToCharArray();
            for (int j = 0; j < 8; j++)
            {
                switch (Pieces[j - displacementCounter])
                {
                    case 'K':
                        if (KW || QW)
                            Output[j + 1, i + 1] = 0b00010110;
                        else
                            Output[j + 1, i + 1] = 0b00010111;
                        break;
                    case 'Q':
                        Output[j + 1, i + 1] = 0b00011000;
                        break;
                    case 'N':
                        Output[j + 1, i + 1] = 0b00010100;
                        break;
                    case 'R':
                        if (j == 0 && QW || j == 7 && KW)
                            Output[j + 1, i + 1] = 0b00011001;
                        else
                            Output[j + 1, i + 1] = 0b00011010;
                        break;
                    case 'B':
                        Output[j + 1, i + 1] = 0b00010101;
                        break;
                    case 'p':
                        if (i == 6)
                            Output[j + 1, i + 1] = 0b00000001;
                        else if (EnPassent && EnPassentCoordinate[0] == i && EnPassentCoordinate[1] == j)
                            Output[j + 1, i + 1] = 0b00000010;
                        else
                            Output[j + 1, i + 1] = 0b00000011;
                        break;

                    case 'k':
                        if (KB || QB)
                            Output[j + 1, i + 1] = 0b00000110;
                        else
                            Output[j + 1, i + 1] = 0b00000111;
                        break;
                    case 'q':
                        Output[j + 1, i + 1] = 0b00001000;
                        break;
                    case 'n':
                        Output[j + 1, i + 1] = 0b00000100;
                        break;
                    case 'r':
                        if (j == 0 && QB || j == 7 && KB)
                            Output[j + 1, i + 1] = 0b00001001;
                        else
                            Output[j + 1, i + 1] = 0b00001010;
                        break;
                    case 'b':
                        Output[j + 1, i + 1] = 0b00000101;
                        break;
                    case 'P':
                        if (i == 1)
                            Output[j + 1, i + 1] = 0b00010001;
                        else if (EnPassent && EnPassentCoordinate[0] == i && EnPassentCoordinate[1] == j)
                            Output[j + 1, i + 1] = 0b00010010;
                        else
                            Output[j + 1, i + 1] = 0b00010011;
                        break;
                    default:
                        CurrentDisplacement = Convert.ToInt32(Pieces[j - displacementCounter]) - 49;
                        j += CurrentDisplacement;
                        displacementCounter += CurrentDisplacement;
                        break;

                }
            }
            displacementCounter = 0;
        }
        //init Turn
        if (Semantics[1] == "w")
            Turn = 1;
        else
            Turn = 0;
        CurrentFen = Fen;
        return Output;
    }
}