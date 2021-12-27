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
    bool Continuing = true;
    bool IsHalfKav2 = true;
    bool IsHalfKp = false;
    bool CurrentState = true;
    Semaphore semaphoreEveryone = new Semaphore(1, 1), Rand = new Semaphore(1, 1), semaphoreTraining = new Semaphore(1, 1);
    Treesearch treesearch = new Treesearch(1, false, 1);
    Random random1 = new Random();
    float CurrentLearningRate = 0;
    float LearningRate;
    NNUE TrainNet = new NNUE();
    public int GameNumber = 1;
    string CurrentNetName = "ValueNet.nnue";
    public Thread[] Playing;
    byte[,] StartingPosition = new byte[9, 9];
    public int gameLength;
    int WinWhite = 0, WinBlack = 0, AmountofGames = 0;
    int FinishedGames = 0;
    int nodeCount;
    int TrainingSteps = 0;
    int PlayingLength = 0;
    int TournamentValues = 0;
    int TournamentGameAmount = 0;
    int gamecounter = 0;
    float Decay = 0;
    float TrainingMomentum = 0;
    bool Tournament = false;
    int LastNumber = 0;
    TrainingPosition[][] CurrentTrainingSet;
    int currentSetCounter = 0;
    bool Savenet = false;

    List<float[][][,]> WeightList = new List<float[][][,]>();
    List<float[][][]> BiasList = new List<float[][][]>();
    List<float[,]> InputMatrixList = new List<float[,]>();

    List<float[][][,]> WeightChanges = new List<float[][][,]>();
    List<float[][][]> BiasChanges = new List<float[][][]>();
    List<float[,]> InputMatrixChanges = new List<float[,]>();

    List<float[][,]> HalfkpWeightCopy = new List<float[][,]>();
    List<float[][]> HalfkpBiasCopy = new List<float[][]>();
    List<float[,]> HalfkpMatrixCopy = new List<float[,]>();
    List<float[]> HalfkpMatrixBiasCopy = new List<float[]>();

    float[][,] CurrentHalfkpWeights = new float[3][,];
    float[][] CurrentHalfkpBiases = new float[3][];
    float[,] CurrentHalfkpMatrix = new float[40960, 256];
    float[] CurrentHalfkpMatrixBiases = new float[512];

    float[][][][,] WeightChangeCopy = new float[0][][][,];
    float[][][][] BiasChangeCopy = new float[0][][][];
    float[][,] MatrixChangeCopy = new float[0][,];

    float[][][,] HalfkpWeightChangeCopy = new float[0][][,];
    float[][][] HalfkpBiaseChangeCopy = new float[0][][];
    float[][,] HalfkpMatrixChangeCopy = new float[0][,];
    float[][] HalfkpMatrixBiaseChangeCopy = new float[0][];

    float[,] TestMatrix = new float[45056, 520];
    float[][][,] TestWeights = new float[8][][,];
    float[][][] TestBiases = new float[8][][];
    float[,] OfficialMatrix = new float[45056, 520];
    float[][][,] OfficialWeights = new float[8][][,];
    float[][][] OfficialBiases = new float[8][][];
    float[][][,] CurrentWeights = new float[8][][,];
    float[][][] CurrentBiases = new float[8][][];
    float[,] CurrentMatrix = new float[45056, 520];
    int ThreadFinished = 0;

    public Training(byte[,] Position, string EvalFileName, int BufferSize, int GameLength, int NodeCount, float LearningCoefficient, int ThreadAmount, float Momentum, int SampleCreateSize, float NetDecay, int startElo, float WinToEvalCoefficient)
    {
        //Change the elo
        OfficialElo = startElo;
        //Set the starting Position
        Array.Copy(Position, StartingPosition, Position.Length);
        //Set Net To Name
        CurrentNetName = EvalFileName;
        //Set Parameters
        Lambda = WinToEvalCoefficient;
        gameLength = GameLength;
        nodeCount = NodeCount;
        LearningRate = LearningCoefficient;
        TrainingMomentum = Momentum;
        PlayingLength = SampleCreateSize;
        Decay = NetDecay;
        //Set the trainnet
        CurrentState = TrainNet.DetectNetType(EvalFileName);
        TrainNet.LoadNet(CurrentNetName , true);
        //init the LearningBuffer
        CurrentTrainingSet = new TrainingPosition[ThreadAmount][];
        CurrentTrainingSet[0] = new TrainingPosition[BufferSize / ThreadAmount];
        if (CurrentState == IsHalfKav2)
        {
            //update the current Weights , Biases , InputLayer Weights and InputLayer Biases
            Array.Copy(TrainNet.Weigths, CurrentWeights, TrainNet.Weigths.Length);
            Array.Copy(TrainNet.StartMatrix, CurrentMatrix, TrainNet.StartMatrix.Length);
            Array.Copy(TrainNet.Biases, CurrentBiases, TrainNet.Biases.Length);
            //Load the test Net
            Array.Copy(TrainNet.Weigths, TestWeights, TrainNet.Weigths.Length);
            Array.Copy(TrainNet.StartMatrix, TestMatrix, TrainNet.StartMatrix.Length);
            Array.Copy(TrainNet.Biases, TestBiases, TrainNet.Biases.Length);
            //Load the Current Official Net
            Array.Copy(TrainNet.Weigths, OfficialWeights, TrainNet.Weigths.Length);
            Array.Copy(TrainNet.StartMatrix, OfficialMatrix, TrainNet.StartMatrix.Length);
            Array.Copy(TrainNet.Biases, OfficialBiases, TrainNet.Biases.Length);
        }
        else if(CurrentState == IsHalfKp)
        {
            Array.Copy(TrainNet.HalfkpWeigths, CurrentHalfkpWeights, TrainNet.HalfkpWeigths.Length);
            Array.Copy(TrainNet.HalfkpBiases, CurrentHalfkpBiases, TrainNet.HalfkpBiases.Length);
            Array.Copy(TrainNet.HalfkpMatrix, CurrentHalfkpMatrix, TrainNet.HalfkpMatrix.Length);
            Array.Copy(TrainNet.HalfkpMatrixBias, CurrentHalfkpMatrixBiases, TrainNet.HalfkpMatrixBias.Length);
        }
        //start Playing Threads
        Playing = new Thread[ThreadAmount];
        Console.WriteLine("Starting to Play...");
        currentSetCounter = CurrentTrainingSet.Length;
        for (int i = 0; i < ThreadAmount; i++)
        {
            Playing[i] = new Thread(GameplayThreadStart);
            Playing[i].Start();
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
        while (true)
        {
            semaphoreEveryone.WaitOne();
            semaphoreTraining.WaitOne();
            if (currentSetCounter == 0)
            {
                //Test
                /*
                treesearch.Test(1000, CurrentTrainingSet[0] , true , false);
                treesearch.ValueNet.SaveHalfkpNet("HalfkpMaybeGood.nnue", true);
                */
                TrainingSteps++;
                //Give the info about the current Trainiing cycle to the user
                Console.WriteLine("Wins for White : {0}", WinWhite);
                Console.WriteLine("Wins for Black : {0}", WinBlack);
                Console.WriteLine("The Finishing Rate is : {0}% ", FinishedGames * 100 / AmountofGames);
                Console.WriteLine("Step number: {0}", TrainingSteps);
                if (TrainingSteps % 1 == 0)
                {
                    cost = TrainNet.NetCost(GetRandomSampleFromBuffer(10000) , CurrentState);
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

                //wait until every Thread is finished
                bool finished = false;
                //Setup the Training set
                currentSetCounter = CurrentTrainingSet.Length;
                while (!finished)
                {
                    ThreadFinished = Playing.Length;
                    foreach (Thread thread in Playing)
                        if (!thread.IsAlive)
                            ThreadFinished--;

                    if (ThreadFinished == 0)
                        finished = true;
                }

                //Setup the Training set
                currentSetCounter = CurrentTrainingSet.Length;
                //Create the new Learning threads
                ThreadFinished = Playing.Length;
                for (int i = 0; i < Playing.Length; i++)
                {
                    semaphoreEveryone.WaitOne();
                    Playing[i] = new Thread(TrainingNet);
                    Playing[i].Start();
                    semaphoreEveryone.Release();
                }
                
                //Saving the Net from the current Neurons and Weights every 50 steps
                if (TrainingSteps % 50 == 0)
                {
                    Console.WriteLine("Saving Current Net!");
                    if (TrainingSteps % 100 != 0)
                        TrainNet.SaveNets(CurrentNetName, CurrentState , true);
                    else
                        TrainNet.SaveNets(GenerateBackupNetName(), CurrentState , false);
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

                if (CurrentState == IsHalfKav2)
                {
                    //Create the copys
                    WeightChangeCopy = new float[WeightChanges.Count][][][,];
                    Array.Copy(WeightChanges.ToArray(), WeightChangeCopy, WeightChangeCopy.Length);
                    BiasChangeCopy = new float[BiasChanges.Count][][][];
                    Array.Copy(BiasChanges.ToArray(), BiasChangeCopy, BiasChangeCopy.Length);
                    MatrixChangeCopy = new float[InputMatrixChanges.Count][,];
                    Array.Copy(InputMatrixChanges.ToArray(), MatrixChangeCopy, MatrixChangeCopy.Length);

                    //Reset the originals
                    WeightChanges = new List<float[][][,]>();
                    BiasChanges = new List<float[][][]>();
                    InputMatrixChanges = new List<float[,]>();

                    //Train the Net
                    TrainNet.setNet(WeightChangeCopy, BiasChangeCopy, MatrixChangeCopy, TrainingMomentum);

                    //update the current Weights and Biases
                    Array.Copy(TrainNet.Weigths, CurrentWeights, CurrentWeights.Length);
                    Array.Copy(TrainNet.Biases, CurrentBiases, CurrentBiases.Length);
                    Array.Copy(TrainNet.StartMatrix, CurrentMatrix, CurrentMatrix.Length);
                }
                else if(CurrentState == IsHalfKp)
                {
                    HalfkpWeightChangeCopy = new float[HalfkpWeightCopy.Count][][,];
                    Array.Copy(HalfkpWeightCopy.ToArray(), HalfkpWeightChangeCopy, HalfkpWeightChangeCopy.Length);
                    HalfkpMatrixChangeCopy = new float[HalfkpMatrixCopy.Count][,];
                    Array.Copy(HalfkpMatrixCopy.ToArray(), HalfkpMatrixChangeCopy, HalfkpMatrixChangeCopy.Length);
                    HalfkpBiaseChangeCopy = new float[HalfkpBiasCopy.Count][][];
                    Array.Copy(HalfkpBiasCopy.ToArray(), HalfkpBiaseChangeCopy, HalfkpBiaseChangeCopy.Length);
                    HalfkpMatrixBiaseChangeCopy = new float[HalfkpMatrixBiasCopy.Count][];
                    Array.Copy(HalfkpMatrixBiasCopy.ToArray(), HalfkpMatrixBiaseChangeCopy, HalfkpMatrixBiaseChangeCopy.Length);

                    HalfkpWeightCopy = new List<float[][,]>();
                    HalfkpMatrixCopy = new List<float[,]>();
                    HalfkpBiasCopy = new List<float[][]>();
                    HalfkpMatrixBiasCopy = new List<float[]>();

                    TrainNet.setHalfkpNet(HalfkpWeightChangeCopy, HalfkpBiaseChangeCopy, HalfkpMatrixChangeCopy, HalfkpMatrixBiaseChangeCopy, TrainingMomentum);

                    Array.Copy(TrainNet.HalfkpWeigths, CurrentHalfkpWeights, TrainNet.HalfkpWeigths.Length);
                    Array.Copy(TrainNet.HalfkpBiases, CurrentHalfkpBiases, TrainNet.HalfkpBiases.Length);
                    Array.Copy(TrainNet.HalfkpMatrix, CurrentHalfkpMatrix, TrainNet.HalfkpMatrix.Length);
                    Array.Copy(TrainNet.HalfkpMatrixBias, CurrentHalfkpMatrixBiases, TrainNet.HalfkpMatrixBias.Length);
                }
                semaphoreEveryone.WaitOne();
                Continuing = true;

               // if (GameNumber % 10000 == 0 || Tournament)
                if(!true)
                {
                    //save the Current Net in the Buffer
                    LastNumber++;
                    Tournament = false;

                    WeightList.Add((float[][][,])CurrentWeights.Clone());
                    BiasList.Add((float[][][])CurrentBiases.Clone());
                    InputMatrixList.Add((float[,])CurrentMatrix.Clone());

                    if (GameNumber % 40000 == 0 || Savenet || LastNumber % 4 == 0)
                    {
                        CreateTestNet(Decay);

                        semaphoreEveryone.Release();

                        if (GatingTest())
                        {
                            Savenet = false;

                            WeightList = new List<float[][][,]>();
                            BiasList = new List<float[][][]>();
                            InputMatrixList = new List<float[,]>();

                            Array.Copy(TestWeights, OfficialWeights, TrainNet.Weigths.Length);
                            Array.Copy(TestMatrix, OfficialMatrix, TrainNet.StartMatrix.Length);
                            Array.Copy(TestBiases, OfficialBiases, TrainNet.Biases.Length);

                            Array.Copy(OfficialWeights, CurrentWeights, TrainNet.Weigths.Length);
                            Array.Copy(OfficialMatrix, CurrentMatrix, TrainNet.StartMatrix.Length);
                            Array.Copy(OfficialBiases, CurrentBiases, TrainNet.Biases.Length);

                            //Update OfficialElo
                            OfficialElo = CurrentChallengerElo;
                            Console.WriteLine("New Net Elo: {0} !", OfficialElo);
                            Console.WriteLine("Saving Main Net!");
                            TrainNet.SaveNet("Official Net.nnue" , true);

                        }
                        else
                            Savenet = true;
                        
                        semaphoreEveryone.WaitOne();

                    }
                }

                semaphoreEveryone.Release();

                Console.WriteLine("                Training successfull !");
                Console.WriteLine("               <=====================>");

                //Reset The threads to playing
                Playing = new Thread[Playing.Length];
                currentSetCounter = CurrentTrainingSet.Length;
                for (int i = 0; i < Playing.Length; i++) 
                {
                    Playing[i] = new Thread(GameplayThreadStart);
                    Playing[i].Start();
                }
            }
            else if(gamecounter >= 10000)
            {
                gamecounter = 0;
                Tournament = true;
                semaphoreTraining.Release();
                semaphoreEveryone.Release();
            }
            else
            {
                semaphoreEveryone.Release();
                semaphoreTraining.Release();
            }
        }
    }
    public float LargeSigmoid(float Input, float Size)
    {
        return (Input / Size) / (float)Math.Sqrt(1 + (Input / Size) * (Input / Size));
    }
    public void CreateTestNet(float Momentum)
    {
        for (int i = 0; i < TestWeights.Length; i++)
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
                    TestMatrix[i, j] = Momentum * InputMatrixList[l][i, j] + (1 - Momentum) * TestMatrix[i, j];

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
        Array.Copy(OfficialWeights, treesearchOfficial.ValueNet.Weigths, CurrentWeights.Length);
        Array.Copy(OfficialBiases, treesearchOfficial.ValueNet.Biases, CurrentBiases.Length);
        Array.Copy(OfficialMatrix, treesearchOfficial.ValueNet.StartMatrix, CurrentMatrix.Length);

        Array.Copy(TestWeights, treesearchTesting.ValueNet.Weigths, CurrentWeights.Length);
        Array.Copy(TestBiases, treesearchTesting.ValueNet.Biases, CurrentBiases.Length);
        Array.Copy(TestMatrix, treesearchTesting.ValueNet.StartMatrix, CurrentMatrix.Length);
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
                    Array.Copy(treesearchOfficial.MonteCarloTreeSim(Board, Color, 300, true, start, true , false , true).Position, Board, Board.Length);
                }
                else
                {
                    ToPlay = true;
                    //Play the Moove
                    Array.Copy(treesearchTesting.MonteCarloTreeSim(Board, Color, 300, true, start, true , false , true).Position, Board, Board.Length);
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
                MateVal = treesearchOfficial.Mate(Board, Color);

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
    public TrainingPosition[] GetRandomSampleFromBuffer(int Length)
    {
        TrainingPosition[] Output = new TrainingPosition[Length];
        for (int i = 0; i < Length; i++)
        {
            Output[i] = new TrainingPosition();
            int CurrentIndex = random1.Next(CurrentTrainingSet[0].Length - 1);
            Array.Copy(CurrentTrainingSet[0][CurrentIndex].Board, Output[i].Board, new byte[9,9].Length);
            Output[i].Color = CurrentTrainingSet[0][CurrentIndex].Color;
            Output[i].Eval = CurrentTrainingSet[0][CurrentIndex].Eval;
        }
        return Output;
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
        int Buffercounter = 0;
        int Length = 0;
        semaphoreEveryone.WaitOne();
        int GameLength = gameLength;
        int NodeCount = nodeCount;
        byte[,] StartBoard = new byte[9, 9];
        Array.Copy(StartingPosition, StartBoard, Board.Length);
        semaphoreEveryone.Release();

        Rand.WaitOne();
        Treesearch treesearchV1 = new Treesearch(random1.Next(Int32.MaxValue), false , 1);
        Rand.Release();

        semaphoreEveryone.WaitOne();
        TrainingPosition[] Buffer = new TrainingPosition[CurrentTrainingSet[0].Length];
        Array.Copy(CurrentWeights, treesearchV1.ValueNet.Weigths, CurrentWeights.Length);
        Array.Copy(CurrentBiases, treesearchV1.ValueNet.Biases, CurrentBiases.Length);
        Array.Copy(CurrentMatrix, treesearchV1.ValueNet.StartMatrix, CurrentMatrix.Length);
        semaphoreEveryone.Release();

        while (Work)
        {
            Array.Copy(StartBoard, Board, Board.Length);
            //Play a game
            List<byte[][,]> Game = new List<byte[][,]>();
            byte Color = 1;
            int Value = 2;
            int MateVal = 2;
            bool start = true;
            int LogStart = treesearchV1.random.Next(0, 20);
            int counter = 0;
            Length++;
            List<float> Eval = new List<float>();
            Length = 0;
            //Play the Game
            for (int i = 0; i < GameLength; i++)
            {
                if (i > 200)
                    start = false;
                //Play the Move
                //Not random
                //use the static Eval function
                simOutput = treesearchV1.MonteCarloTreeSim(Board, Color, NodeCount, NewStart, start, false , false , false);
                Array.Copy(simOutput.Position, Board, Board.Length);
                NewStart = false;
                //random
                //Board = treesearchV2.RandomSim(Board, Color);
                //Change the Color
                if (Color == 0)
                    Color = 1;
                else
                    Color = 0;
                //Check for Mate
                MateVal = treesearchV1.Mate(Board, Color);

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
                        Position.Eval = (1 - Lambda) * Value + Lambda * LargeSigmoid(Eval[counter], 4.2f);
                    else
                        Position.Eval = LargeSigmoid(Eval[counter], 4);

                    if (Buffercounter < Buffer.Length)
                    {
                        Buffer[Buffercounter] = new TrainingPosition();
                        Array.Copy(Position.Board, Buffer[Buffercounter].Board, Position.Board.Length);
                        Buffer[Buffercounter].Color = Position.Color;
                        Buffer[Buffercounter].Eval = Position.Eval;
                        Buffercounter++;
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
                    if (MateVal == 50)
                        WinWhite++;
                    else if (MateVal == -50)
                        WinBlack++;
                }

                //Externalize The Game stats
                Console.WriteLine("New Game ( Number : {2} / Score : {0} / Length : {1})", MateVal / 50, Length, GameIteration);

                semaphoreEveryone.WaitOne();
                gamecounter++;
                //Work = Continuing;
                NewStart = true;
                semaphoreEveryone.Release();
            }
        }
        semaphoreEveryone.WaitOne();
        currentSetCounter--;
        CurrentTrainingSet[currentSetCounter] = Buffer;
        semaphoreEveryone.Release();
    }
    public void TrainingNet()
    {
        bool State = CurrentState;
        //Get the training Information
        semaphoreEveryone.WaitOne();
        currentSetCounter--;
        TrainingPosition[] TrainingArray;
        TrainingArray = new TrainingPosition[CurrentTrainingSet[currentSetCounter].Length];
        Array.Copy(CurrentTrainingSet[currentSetCounter], TrainingArray, TrainingArray.Length);
        float LearningRate = CurrentLearningRate;
        //Create the current Neural Net
        NNUE Net = new NNUE();
        //Set it to the Neural Net
        if (State == IsHalfKav2)
        {
            Array.Copy(CurrentWeights, Net.Weigths, CurrentWeights.Length);
            Array.Copy(CurrentBiases, Net.Biases, CurrentBiases.Length);
            Array.Copy(CurrentMatrix, Net.StartMatrix, CurrentMatrix.Length);
        }
        else
        {
            Array.Copy(CurrentHalfkpWeights, Net.HalfkpWeigths, CurrentHalfkpWeights.Length);
            Array.Copy(CurrentHalfkpBiases, Net.HalfkpBiases, CurrentHalfkpBiases.Length);
            Array.Copy(CurrentHalfkpMatrix, Net.HalfkpMatrix, CurrentHalfkpMatrix.Length);
            Array.Copy(CurrentHalfkpMatrixBiases, Net.HalfkpMatrixBias, CurrentHalfkpMatrixBiases.Length);
        }
        semaphoreEveryone.Release();

        //Backpropagate the Part
        if (State)
            Net.BackPropagation2(TrainingArray, LearningRate);
        else
            Net.BackPropagationHalfkp(TrainingArray, LearningRate);

        //Save the Results
        semaphoreTraining.WaitOne();
        if (State)
        {
            WeightChanges.Add(Net.WeigthChanges);
            BiasChanges.Add(Net.BiasChange);
            InputMatrixChanges.Add(Net.MatrixChange);
        }
        else
        {
            HalfkpWeightCopy.Add(Net.HalfkpWeigthChanges);
            HalfkpBiasCopy.Add(Net.HalfkpBiasChange);
            HalfkpMatrixCopy.Add(Net.HalfkpMatrixChange);
            HalfkpMatrixBiasCopy.Add(Net.HalfkpMatrixBiasChange);
        }
        semaphoreTraining.Release();
    }
}
class TrainingPosition
{
    public byte[,] Board = new byte[9, 9];
    public byte Color = 0;
    public float Eval = 0;
}
