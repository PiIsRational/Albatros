using System;
using System.Collections.Generic;
using System.Threading;


class Game
{
    public static string StartPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    public Position board = new Position();
    public static bool quit = false;
    public bool Playing = false;
    public string CurrentFen;
    static Thread CommandExecute;
    static Io io = new Io();
    static string output = "";
    static string CommandBuffer = "";

    //Training Parameters
    public int depthPly = 3;
    public int Elo = 100;
    public float Lambda = 1;
    public int buffer_size = 50000000;
    public int batch_size = 32000;
    public int GameLength = 1000;
    public string NetName = "ValueNet.nnue";
    public int NodeCount = 10;
    public float learning_rate = 0.0001f;
    public float Momentum = 0.9f;
    public float NetDecay = 0.75f;
    public bool Play = true;
    public string LogFile = "BufferLog";

    //Other Parameters
    public float c_puct = 10;
    public bool IsPlaying = false;
    public bool NNUE = true;
    public int ThreadCount = 5;
    public int HashSize = 18;
    public bool USE_MCTS = false;
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
        Console.WriteLine("ready");
    }
    public void SetOutput(string Input)
    {
        output = Input;
    }
    static void Update()
    {
        string Command = "";
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
            Command = Console.ReadLine();
            
            List<string> input = io.SyntaxWithoutHoles(Command.Split(' '));
            if (input.Count != 0 && input[0] == "stop")
            {
                io.Stop();
            }
            else if (input.Count != 0 && input[0] == "quit")
            {
                io.Stop();
                quit = true;
            }
            else if (input.Count != 0 && input[0] == "Training")
                CommandExecute = new Thread(io.ThreadStart);
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
}