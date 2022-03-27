using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;

class Training
{
    int CurrentChallengerElo = 100;
    int OfficialElo = 100;
    float Lambda = 1;
    bool Continuing = true, IsPlaying = true;
    Semaphore semaphoreEveryone = new Semaphore(1, 1), Rand = new Semaphore(1, 1), semaphoreTraining = new Semaphore(1, 1);
    StreamWriter position_logger;
    StreamReader position_reader;
    Treesearch treesearch = new Treesearch(1, false, 1);
    Random random1 = new Random();
    float CurrentLearningRate = 0;
    float LearningRate;
    NNUE TrainNet = new NNUE();
    public int GameNumber = 1;
    string CurrentNetName = "ValueNet.nnue", logFile = "";
    public Thread[] Playing;
    byte[,] StartingPosition = new byte[9, 9];
    public int gameLength;
    int WinWhite = 0, WinBlack = 0, AmountofGames = 0, FinishedGames = 0, nodeCount, TrainingSteps = 0, PlayingLength = 0, TournamentValues = 0, TournamentGameAmount = 0, gamecounter = 0, bufferSize = 0;
    public int Currentphase = 0;
    float TrainingMomentum = 0;
    int online_thread_counter = 0;
    int log_pointer = 0, Batch_size = 0;
    int depthply = 0;

    List<double[][,]> HalfkpWeightCopy = new List<double[][,]>();
    List<double[][]> HalfkpBiasCopy = new List<double[][]>();
    List<double[,]> HalfkpMatrixCopy = new List<double[,]>();
    List<double[]> HalfkpMatrixBiasCopy = new List<double[]>();

    double[][,] CurrentHalfkpWeights = new double[1][,];
    double[][] CurrentHalfkpBiases = new double[1][];
    double[,] CurrentHalfkpMatrix = new double[768, 128];
    double[] CurrentHalfkpMatrixBiases = new double[128];

    double[][][,] HalfkpWeightChangeCopy = new double[0][][,];
    double[][][] HalfkpBiaseChangeCopy = new double[0][][];
    double[][,] HalfkpMatrixChangeCopy = new double[0][,];
    double[][] HalfkpMatrixBiaseChangeCopy = new double[0][];

    int ThreadFinished = 0;

    public Training(byte[,] Position, string EvalFileName, int BufferSize, int GameLength, int NodeCount, float LearningCoefficient, int ThreadAmount, float Momentum, int batch_size, float WinToEvalCoefficient, bool Play, string LogFile , int depthPly)
    {
        //Set the starting Position
        Array.Copy(Position, StartingPosition, Position.Length);
        //Set Net To Name
        CurrentNetName = EvalFileName;
        //Set Parameters
        depthply = depthPly;
        Batch_size = batch_size;
        Lambda = WinToEvalCoefficient;
        gameLength = GameLength;
        nodeCount = NodeCount;
        LearningRate = LearningCoefficient;
        IsPlaying = Play;
        logFile = LogFile;
        TrainingMomentum = Momentum;
        PlayingLength = BufferSize / batch_size;
        bufferSize = BufferSize;

        //Set the trainnet
        try
        {
            TrainNet.OpenNet(CurrentNetName);
        }
        catch { }
        //init the LearningBuffer

        Array.Copy(TrainNet.HalfkpWeigths, CurrentHalfkpWeights, TrainNet.HalfkpWeigths.Length);
        Array.Copy(TrainNet.HalfkpBiases, CurrentHalfkpBiases, TrainNet.HalfkpBiases.Length);
        Array.Copy(TrainNet.HalfkpMatrix, CurrentHalfkpMatrix, TrainNet.HalfkpMatrix.Length);
        Array.Copy(TrainNet.HalfkpMatrixBias, CurrentHalfkpMatrixBiases, TrainNet.HalfkpMatrixBias.Length);

        //init the change Arrays
        HalfkpWeightChangeCopy = new double[ThreadAmount][][,];
        HalfkpMatrixChangeCopy = new double[ThreadAmount][,];
        HalfkpBiaseChangeCopy = new double[ThreadAmount][][];
        HalfkpMatrixBiaseChangeCopy = new double[ThreadAmount][];

        //init Playing Threads
        Playing = new Thread[ThreadAmount];

        if (IsPlaying)
        {
            start_logging(logFile);
            //start Playing Threads
            Console.WriteLine("Starting to Play...");
            online_thread_counter = Playing.Length;
            for (int i = 0; i < ThreadAmount; i++)
            {
                Playing[i] = new Thread(GameplayThreadStart);
                Playing[i].Start();
            }
        }
        else
        {
            //open the Buffer from a file
            start_reading(logFile);
            Console.WriteLine("opened position dialog...");
        }
        ManageTraining();
    }
    public string GenerateBackupNetName()
    {
        return "NNUE Backup/nn-" + Convert.ToString(random1.Next(0, int.MaxValue)) + ".nnue";
    }
    public void ManageTraining()
    {
        double[] cost;
        TrainingPosition[] TestPositions = LoadPositionsFromFile("BufferLog-7000000.txt");

        cost = TrainNet.CostOfNet(TestPositions);
        Console.WriteLine("The Mean Error is : {0} , It should be smaller then {1}", cost[0], cost[1]);

        semaphoreEveryone.WaitOne();
        semaphoreTraining.WaitOne();
        bool finished = false;
        if (online_thread_counter == 0)
        {
            //End of Play
            if (IsPlaying)
            {
                stop_logging();
                Console.WriteLine("finished game generation!");

                //Give the info about the current Training cycle to the user
                Console.WriteLine("Wins for White : {0}", WinWhite);
                Console.WriteLine("Wins for Black : {0}", WinBlack);
                Console.WriteLine("The Finishing Rate is : {0}% ", FinishedGames * 100 / AmountofGames);
                Console.WriteLine("Step number: {0}", TrainingSteps);
                if (TrainingSteps % 1 == 0)
                {
                    cost = TrainNet.CostOfNet(TestPositions);
                    Console.WriteLine("The Mean Error is : {0} , It should be smaller then {1}", cost[0], cost[1]);
                    //Log info
                    LogNumbers(cost, TrainingSteps);
                }
                CurrentLearningRate = LearningRate;
                WinWhite = 0;
                WinBlack = 0;
                AmountofGames = 0;
                FinishedGames = 0;
                ThreadFinished = Playing.Length;
                semaphoreTraining.Release();

                //Stop the PlayingThreads
                Continuing = false;
                semaphoreEveryone.Release();
            }
            //Start of Training
            else
            {
                semaphoreEveryone.Release();
                semaphoreTraining.Release();
                //Setup the Training set
                online_thread_counter = Playing.Length;
                for (int TrainingTimes = 0; TrainingTimes < 1000; TrainingTimes++)
                {
                    for (int i = 0; i < PlayingLength; i++)
                    {
                        online_thread_counter = Playing.Length;
                        Currentphase = i;
                        //Create the new Learning threads
                        ThreadFinished = Playing.Length;
                        for (int j = 0; j < Playing.Length; j++)
                        {
                            semaphoreEveryone.WaitOne();
                            Playing[j] = new Thread(TrainingNet);
                            Playing[j].Start();
                            semaphoreEveryone.Release();
                        }
                        if (i == 0)
                        {
                            //Saving the Net from the current Neurons and Weights

                            Console.WriteLine("Saving Current Net!");
                            if (TrainingSteps % 2 == 0)
                                TrainNet.SaveNet(CurrentNetName, true);
                            else
                                TrainNet.SaveNet(GenerateBackupNetName(), false);
                        }
                        //wait until every Net is finished
                        finished = false;
                        while (!finished)
                        {
                            ThreadFinished = Playing.Length;
                            foreach (Thread thread in Playing)
                                if (!thread.IsAlive)
                                    ThreadFinished--;

                            if (ThreadFinished == 0)
                                finished = true;
                        }

                        Array.Copy(HalfkpWeightCopy.ToArray(), HalfkpWeightChangeCopy, HalfkpWeightChangeCopy.Length);
                        Array.Copy(HalfkpMatrixCopy.ToArray(), HalfkpMatrixChangeCopy, HalfkpMatrixChangeCopy.Length);
                        Array.Copy(HalfkpBiasCopy.ToArray(), HalfkpBiaseChangeCopy, HalfkpBiaseChangeCopy.Length);
                        Array.Copy(HalfkpMatrixBiasCopy.ToArray(), HalfkpMatrixBiaseChangeCopy, HalfkpMatrixBiaseChangeCopy.Length);

                        HalfkpWeightCopy = new List<double[][,]>();
                        HalfkpMatrixCopy = new List<double[,]>();
                        HalfkpBiasCopy = new List<double[][]>();
                        HalfkpMatrixBiasCopy = new List<double[]>();

                        TrainNet.GradientDescent(HalfkpWeightChangeCopy, HalfkpBiaseChangeCopy, HalfkpMatrixChangeCopy, HalfkpMatrixBiaseChangeCopy, TrainingMomentum, LearningRate, 0.00001f, 1000);

                        CurrentHalfkpWeights = TrainNet.HalfkpWeigths;
                        CurrentHalfkpBiases = TrainNet.HalfkpBiases;
                        CurrentHalfkpMatrix = TrainNet.HalfkpMatrix;
                        CurrentHalfkpMatrixBiases = TrainNet.HalfkpMatrixBias;
                    }

                    cost = TrainNet.CostOfNet(TestPositions);
                    Console.WriteLine("The Mean Error is : {0} , It should be smaller then {1}", cost[0], cost[1]);
                    //Log info
                    LogNumbers(cost, TrainingSteps);

                }
                semaphoreEveryone.WaitOne();
                Continuing = true;

                semaphoreEveryone.Release();

                Console.WriteLine("                Training successfull !");
                Console.WriteLine("               <=====================>");
                stop_reading();
            }
        }
    }
    public float LargeSigmoid(float Input, float Size)
    {
        return (Input / Size) / (float)Math.Sqrt(1 + (Input / Size) * (Input / Size));
    }
    public void CreateTestNet(float Momentum)
    {
        /*for (int i = 0; i < TestWeights.Length; i++)
        {

            for (int j = 0; j < TestWeights[i].Length; j++)
            {
                //Layer
                for (int k = 0; k < TestWeights[i][j].GetLength(1); k++)
                {
                    //Neuron
                    for (int m = 0; m < TestWeights[i][j].GetLength(0); m++)
                    {
                        //Set New Weight
                        for (int l = 0; l < WeightList.Count; l++)
                            TestWeights[i][j][m, k] = Momentum * WeightList[l][i][j][m, k] + (1 - Momentum) * TestWeights[i][j][m, k];
                    }
                    //Set New Bias
                    for (int l = 0; l < BiasList.Count; l++)
                        TestBiases[i][j][k] = Momentum * BiasList[l][i][j][k] + (1 - Momentum) * TestBiases[i][j][k];
                }
            }
        }

        for (int i = 0; i < TestMatrix.GetLength(0); i++)
            for (int j = 0; j < TestMatrix.GetLength(1); j++)
                for (int l = 0; l < BiasList.Count; l++)
                    TestMatrix[i, j] = Momentum * InputMatrixList[l][i, j] + (1 - Momentum) * TestMatrix[i, j];*/

    }
    public void start_logging(string Filename)
    {
        string File = Filename + "-" + bufferSize + ".positions";
        position_logger = new StreamWriter(File);
    }
    public void start_reading(string Filename)
    {
        string File = Filename + "-" + bufferSize + ".positions";
        position_reader = new StreamReader(File);
    }
    public TrainingPosition[] GetTrainingPositions(int array_size, int skip_size , string Filename)
    {
        string position_string = "";
        Random random = new Random();
        TrainingPosition[] positions = new TrainingPosition[array_size];
        for (int i = 0; i < array_size; i++)
        {
            log_pointer ++;
            if (log_pointer == bufferSize)
            {
                stop_reading();
                start_reading(Filename);
                log_pointer = log_pointer % bufferSize;
            }
            for (int j = 0;j < random.Next(0, 2 * skip_size); j++)
            {
                position_string = position_reader.ReadLine();
                log_pointer++;
                if (log_pointer == bufferSize - 1)
                    break;
            }
            positions[i] = stringToTrainingPosition(position_string);
        }
        return positions;
    }
    public void log_position_to_file(TrainingPosition position)
    {
        position_logger.WriteLine(BoardToString(position.Board, position.Color) + BitConverter.SingleToInt32Bits(position.Eval));
    }
    public void stop_reading()
    {
        position_reader.Close();
    }
    public void stop_logging()
    {
        position_logger.Flush();
        position_logger.Close();
    }
    public TrainingPosition stringToTrainingPosition(string Input)
    {
        TrainingPosition Output = new TrainingPosition();
        char[] InputCharArray = Input.ToCharArray();
        int counter = 0;
        for (int i = 1; i < 9; i++)
        {
            for (int j = 1; j < 9; j++)
            {
                Output.Board[i, j] = (byte)(InputCharArray[counter] - 97);
                counter++;
            }
        }
        Output.Color = (byte)(InputCharArray[counter] - 48);
        counter++;

        string Eval = "";

        for (int i = counter; i < InputCharArray.Length; i++)
            Eval += InputCharArray[i];

        Output.Eval = BitConverter.Int32BitsToSingle(Convert.ToInt32(Eval));

        return Output;
    }
    public TrainingPosition[] LoadPositionsFromFile(string Filename)
    {
        int FileSize = Convert.ToInt32(Filename.Split('.', '-')[1]);
        StreamReader sr = new StreamReader(Filename);
        TrainingPosition[] Positions = new TrainingPosition[FileSize];

        for (int i = 0; i < FileSize; i++)
            Positions[i] = stringToTrainingPosition(sr.ReadLine());

        return Positions;
    }
    public string BoardToString(byte[,] Board , byte color)
    {
        string Output = "";
        for (int i = 1; i < 9; i++)
        {
            for (int j = 1; j < 9; j++)
            {
                Output += (char)(Board[i, j] + 97);
            }
        }
        return Output + color;
    }
    public bool GatingTest()
    {
        CurrentChallengerElo = OfficialElo;
        bool GamesFinished = false;
        bool finished = false;
        bool Output = false;
        TournamentValues = 0;
        TournamentGameAmount = 0;
        Continuing = true;
        ThreadFinished = Playing.Length;
        for (int i = 0; i < Playing.Length; i++)
        {
            semaphoreEveryone.WaitOne();
            Playing[i] = new Thread(GatingPlayThreadStart);
            Playing[i].Start();
            semaphoreEveryone.Release();
        }
        while (!GamesFinished)
        {
            semaphoreEveryone.WaitOne();
            if (TournamentGameAmount == 30)
            {
                GamesFinished = true;
                Continuing = false;
            }
            semaphoreEveryone.Release();
        }
        //wait until every Net is finished
        while (!finished)
        {
            ThreadFinished = Playing.Length;
            foreach (Thread thread in Playing)
                if (!thread.IsAlive)
                    ThreadFinished--;

            if (ThreadFinished == 0)
                finished = true;
        }
        if(TournamentValues > 30)
        {
            Output = true;
            Console.WriteLine("Passed Gating Test With a Score of {0} / 30", ((double)TournamentValues) / 2);
        }
        else
            Console.WriteLine("Did Not Pass Gating Test With a Score of {0} / 30", ((double)TournamentValues) / 2);

        //Elo is Missing
        LogGating(((float)TournamentValues) / 2, CurrentChallengerElo);

        Continuing = true;

        return Output;
    }
    public void LogGating(float TournamentValue , int Elo)
    {
        StreamReader reader = new StreamReader("Log.txt");
        string file = reader.ReadToEnd();
        reader.Close();
        StreamWriter writer = new StreamWriter("Log.txt");
        writer.Write(file);
        writer.WriteLine("Tournament Score: {0} , Elo: {1}", TournamentValue, Elo);
        writer.Close();
    }
    public int CalculateElo(int Score, int Elo)
    {
        double Expected = 1 / (1 + Math.Pow(10, (OfficialElo - Elo) / 400));
        int NewRating = (int)(Elo + 40 * ((Score + 1) / 2 - Expected));
        return NewRating;
    }
    public void GatingPlayThreadStart()
    {
        bool start = true;
        bool ToPlay = true;
        bool WhoStarts = true;
        bool Work = true;
        byte[,] Board = new byte[9, 9];
        bool StartfromTranspositionBoard = false;
        semaphoreEveryone.WaitOne();
        int GameLength = gameLength;
        int NodeCount = nodeCount;
        byte[,] StartBoard = new byte[9, 9];
        Array.Copy(StartingPosition, StartBoard, Board.Length);
        byte[,] TranspositionBoard = new byte[9, 9];
        bool startLater = false;
        semaphoreEveryone.Release();

        Rand.WaitOne();
        Treesearch treesearchOfficial = new Treesearch(random1.Next(Int32.MaxValue), false , 1);
        Treesearch treesearchTesting = new Treesearch(random1.Next(Int32.MaxValue), false , 1);
        Rand.Release();

        semaphoreEveryone.WaitOne();
        /*Array.Copy(OfficialWeights, treesearchOfficial.ValueNet.Weigths, CurrentWeights.Length);
        Array.Copy(OfficialBiases, treesearchOfficial.ValueNet.Biases, CurrentBiases.Length);
        Array.Copy(OfficialMatrix, treesearchOfficial.ValueNet.StartMatrix, CurrentMatrix.Length);

        Array.Copy(TestWeights, treesearchTesting.ValueNet.Weigths, CurrentWeights.Length);
        Array.Copy(TestBiases, treesearchTesting.ValueNet.Biases, CurrentBiases.Length);
        Array.Copy(TestMatrix, treesearchTesting.ValueNet.StartMatrix, CurrentMatrix.Length);*/
        semaphoreEveryone.Release();

        while (Work)
        {
            Array.Copy(StartBoard, Board, Board.Length);

            //Play a game
            byte Color = 1;
            int Value = 0;
            int MateVal = 2;
            int counter = 0;
            start = true;
            //Determine Who starts
            if (WhoStarts)
            {
                ToPlay = false;
                WhoStarts = false;
            }
            else
            {
                ToPlay = true;
                WhoStarts = true;
            }

            if (StartfromTranspositionBoard)
            {
                startLater = true;
                StartfromTranspositionBoard = false;
                Array.Copy(TranspositionBoard, Board, Board.Length);
            }
            else
                StartfromTranspositionBoard = true;
            
            //Play the Game
            for (int i = 0; i < GameLength; i++)
            {
                if (startLater)
                {
                    i = 10;
                    startLater = false;
                }
                if (i > 9)
                    start = false;

                if (i == 10 && StartfromTranspositionBoard)
                    Array.Copy(Board, TranspositionBoard, Board.Length);

                counter = i + 1;

                if (ToPlay)
                {
                    ToPlay = false;
                    //Play the Moove
                    Array.Copy(treesearchOfficial.MonteCarloTreeSim(Board, Color, 300, true, start, false, true, 1, true, 2).Position, Board, Board.Length);
                }
                else
                {
                    ToPlay = true;
                    //Play the Moove
                    Array.Copy(treesearchTesting.MonteCarloTreeSim(Board, Color, 300, true, start, false, true, 1, true, 2).Position, Board, Board.Length);
                }

                if (Color == 0)
                    Color = 1;
                else
                    Color = 0;

                semaphoreEveryone.WaitOne();
                Work = Continuing;
                if (!Work)
                {
                    semaphoreEveryone.Release();
                    break;
                }
                semaphoreEveryone.Release();

                //Check for Mate
                MateVal = treesearchOfficial.MoveGenerator.Mate(Board, Color);

                if (MateVal != 2)
                {
                    Value = MateVal;
                    break;
                }
            }
            semaphoreEveryone.WaitOne();
            if (Work)
            {
                int GameIteration = GameNumber;

                if (WhoStarts)
                    Value = -Value;

                if (MateVal != 2)
                {
                    Value++;
                    CurrentChallengerElo = CalculateElo(Value / 2, CurrentChallengerElo);
                    TournamentGameAmount++;
                }
                else
                    Value = 4;
                if (Value / 2 == 1)
                    Console.ForegroundColor = ConsoleColor.Green;
                else
                    if (Value == 0)
                    Console.ForegroundColor = ConsoleColor.Red;
                //Externalize The Game stats
                Console.WriteLine("New Game Test vs Official (Number: {2} / Score: {0} / Length: {1})", ((double)Value) / 2, counter, GameIteration);
                Console.ForegroundColor = ConsoleColor.White;
                GameIteration++;
                GameNumber = GameIteration;
                Work = Continuing;
                if (Value != 4)
                    TournamentValues += Value;
            }
            semaphoreEveryone.Release();
        }
    }
    public void LogNumbers(double[] Inputerror, int iteration)
    {
        string[] Number = (Convert.ToString(Inputerror[0]) + "," + Convert.ToString(Inputerror[1])).Split(',');
        string[] Performance = Convert.ToString(Inputerror[0] - Inputerror[1]).Split(',');
        StreamReader reader = new StreamReader("Numbers.txt");
        string file = reader.ReadToEnd();
        reader.Close();
        StreamWriter writer = new StreamWriter("Numbers.txt");
        writer.Write(file);
        writer.WriteLine("Iterarion: {0} , Error: {1}.{2} Must: {3}.{4} Performance: {5}.{6}", iteration, Number[0], Number[1], Number[2], Number[3], Performance[0], Performance[1]);
        writer.Close();
    }
    public void GameplayThreadStart()
    {
        MCTSimOutput simOutput = new MCTSimOutput();
        TrainingPosition Position = new TrainingPosition();
        bool NewStart = true;
        bool Work = true;
        byte[,] Board = new byte[9, 9];
        int buffercounter = 0;
        int Length = 0;
        semaphoreEveryone.WaitOne();
        int GameLength = gameLength;
        int NodeCount = nodeCount;
        int depthPly = depthply;
        byte[,] StartBoard = new byte[9, 9];
        Array.Copy(StartingPosition, StartBoard, Board.Length);
        semaphoreEveryone.Release();

        Rand.WaitOne();
        Treesearch treesearchV1 = new Treesearch(random1.Next(Int32.MaxValue), false , 1);
        Rand.Release();

        semaphoreEveryone.WaitOne();
        int buffer_length = bufferSize;
        semaphoreEveryone.Release();

        while (Work)
        {
            Array.Copy(StartBoard, Board, Board.Length);
            //Play a game
            List<byte[][,]> Game = new List<byte[][,]>();
            byte Color = 1;
            int Value = 2;
            int MateVal = 2;
            int LogStart = treesearchV1.random.Next(10, 20);
            int counter = 0;
            Length++;
            List<float> Eval = new List<float>();
            Length = 0;
            //Play the Game
            for (int i = 0; i < GameLength; i++)
            {
                /*if (i > 200)
                    start = false;*/
                //Play the Move
                //use the static Eval function
                if (i >= LogStart)
                {
                    simOutput = treesearchV1.MonteCarloTreeSim(Board, Color, NodeCount, NewStart, false, false, false, 2, true, depthPly);
                    Array.Copy(simOutput.Position, Board, Board.Length);
                }
                //start with random play
                else
                    Board = treesearchV1.RandomSim(Board, Color);

                NewStart = false;

                //Change the Color
                Color = (byte)(1 - Color);

                //Check for Mate
                MateVal = treesearchV1.MoveGenerator.Mate(Board, Color);

                if (MateVal != 2 && MateVal !=-2)
                {
                    Value = MateVal;
                    break;
                }
                //add the board
                byte[,] BoardCopy = new byte[9, 9];
                if (i >= LogStart)
                {
                    Array.Copy(Board, BoardCopy, Board.Length);
                    if (i > LogStart)
                        Eval.Add(simOutput.eval);
                    Game.Add(new byte[][,] { BoardCopy, new byte[,] { { Color } } });
                }
                Length++;
            }
            //Input the Last Value
            if (Value == MateVal && Value != 2 && Eval.Count >= 1)
                Eval.Add(Value);
            else
                Eval.Add(treesearchV1.eval.PestoEval(Board, Color));

            if (Work)
            {
                semaphoreEveryone.WaitOne();
                GameNumber++;
                int GameIteration = GameNumber;
                if (Value != 2)
                    FinishedGames++;
                semaphoreEveryone.Release();

                //Add the Game to the Buffer
                foreach (byte[][,] Example in Game)
                {
                    //The result is stored from the point of view of Our Enemy
                    Value = -Value;
                    Array.Copy(Example[0], Position.Board, Example[0].Length);
                    Position.Color = Example[1][0,0];

                    if (MateVal != 2)
                        Position.Eval = (1 - Lambda) * Value + Lambda * Eval[counter];
                    else
                        Position.Eval = Eval[counter];

                    if (buffercounter < buffer_length)
                    {
                        //log to the file 
                        semaphoreEveryone.WaitOne();
                        log_position_to_file(Position);
                        semaphoreEveryone.Release();
                        buffercounter++;
                    }
                    else
                    {
                        Work = false;
                        break;
                    }
                    counter++;
                }

                AmountofGames++;
                if (MateVal != 2)
                {
                    if (MateVal == 1)
                        WinWhite++;
                    else if (MateVal == -1)
                        WinBlack++;
                }

                //Externalize The Game stats
                Console.WriteLine("New Game (Number {2} / Score {0} / Length {1})", MateVal, Length, GameIteration);

                semaphoreEveryone.WaitOne();
                gamecounter++;
                //Work = Continuing;
                NewStart = true;
                semaphoreEveryone.Release();
            }
        }
        semaphoreEveryone.WaitOne();
        online_thread_counter--;
        semaphoreEveryone.Release();
    }
    public void TrainingNet()
    {
        //Get the training Information
        Random random = new Random();
        semaphoreEveryone.WaitOne();
        online_thread_counter--;

        //get the current training array
        TrainingPosition[] TrainingArray = GetTrainingPositions(Batch_size, 10, logFile);

        float LearningRate = CurrentLearningRate;

        //Create the current Neural Net
        NNUE Net = new NNUE();

        //Set it to the Neural Net
        Net.HalfkpWeigths = CurrentHalfkpWeights;
        Net.HalfkpBiases = CurrentHalfkpBiases;
        Net.HalfkpMatrix = CurrentHalfkpMatrix;
        Net.HalfkpMatrixBias = CurrentHalfkpMatrixBiases;

        semaphoreEveryone.Release();

        //Backpropagate the Part
        Net.BackPropagation(TrainingArray);

        //Save the Results
        semaphoreTraining.WaitOne();

        HalfkpWeightCopy.Add(Net.HalfkpWeigthChanges);
        HalfkpBiasCopy.Add(Net.HalfkpBiasChange);
        HalfkpMatrixCopy.Add(Net.HalfkpMatrixChange);
        HalfkpMatrixBiasCopy.Add(Net.HalfkpMatrixBiasChange);

        semaphoreTraining.Release();
    }
}
class TrainingPosition
{
    public byte[,] Board = new byte[9, 9];
    public byte Color = 0;
    public float Eval = 0;
}
