﻿
using BoosterBot.Models;
using System.Diagnostics;

namespace BoosterBot
{
    internal class LadderBot : BaseBot
    {
        public LadderBot(BotConfig config, int retreat) : base(config, retreat) { }

        public void Debug()
        {
            while (true)
            {
                try
                {
                    var print = _game.LogLadderGameState();
                    foreach (var line in print.Logs) Console.WriteLine(line);
                    foreach (var line in print.Results) Console.WriteLine(line);
                    Console.WriteLine("--------------------------------------------------------");
                    Console.WriteLine("--------------------------------------------------------");

                    Thread.Sleep(5000);
                }
                catch (Exception ex)
                {

                    Thread.Sleep(5000);
                }
            }
        }

        public override void Run()
        {
            Log("Ladder_Log_Start");
            var attempts = 0;

            while (true)
            {
                attempts++;

                _config.GetWindowPositions();
                _game.ResetClick();
                _game.ResetMenu();
                _game.ResetClick();

                Thread.Sleep(500);

                _config.GetWindowPositions();
                var onMenu = Check(_game.CanIdentifyMainMenu);
                if (onMenu)
                    DetermineLoopEntryPoint();
                else
                {
                    if (attempts <= 2)
                    {
                        Log("Log_FailedMenuDetection");
                        _game.ResetClick();
                        Thread.Sleep(5000);
                    }
                    else
                    {
                        attempts = 0;
                        DetermineLoopEntryPoint();
                    }
                }
            }
        }

        private bool DetermineLoopEntryPoint(int attempts = 0)
        {
            Log("Log_LoopEntryPoint");
            var state = _game.DetermineLadderGameState();

            switch (state)
            {
                case GameState.MAIN_MENU:
                    Log("Log_DetectedMainMenu");
                    StartMatch();
                    return true;
                case GameState.RECONNECT_TO_GAME:
                    Log("Log_DetectedReconnect");
                    _game.ClickPlay();
                    Thread.Sleep(4000);
                    return PlayMatch();
                case GameState.MID_MATCH:
                    Log("Log_DetectedActiveMatch");
                    return StartMatch();
                case GameState.LADDER_MATCHMAKING:
                    Log("Log_DetectedMatchmaking");
                    return WaitForMatchmaking();
                case GameState.LADDER_MATCH:
                    Log("Log_DetectedActiveMatch");
                    return PlayMatch();
                case GameState.LADDER_MATCH_END:
                case GameState.LADDER_MATCH_END_REWARDS:
                    Log("Log_DetectedMatchEnd");
                    return ExitMatch();
                case GameState.CONQUEST_LOBBY_PG:
                    Log("Log_Ladder_DetectedConquest");
                    _game.ResetMenu();
                    return StartMatch();
                default:
                    if (attempts < 5)
                    {
                        _game.BlindReset();
                        return DetermineLoopEntryPoint(attempts + 1);
                    }

                    Log("Log_LostBot");
                    Log("Log_LostBot_Restart");
                    Console.WriteLine();
                    Log("Menu_PressKeyToExit");
                    Console.ReadKey();
                    Environment.Exit(0);
                    return false;
            }
        }

        private bool StartMatch()
        {
            Log("Log_Match_StartNew");
            _game.ClickPlay();
            for (int i = 0; i < 10 && !_isStopped; i++)
            {
                Thread.Sleep(1000);
            }
            _game.ClickPlay(); // Press a second time just to be sure
            for (int i = 0; i < 10 && !_isStopped; i++)
            {
                Thread.Sleep(3000);
            }

            return WaitForMatchmaking();
        }

        private bool WaitForMatchmaking()
        {
            _config.GetWindowPositions();

            var mmTimer = new Stopwatch();
            mmTimer.Start();

            Log("Log_Check_Matchmaking", true);
            while (Check(() => _game.CanIdentifyLadderMatchmaking()))
            {
                if (mmTimer.Elapsed.TotalSeconds > _rand.Next(300, 360))
                {
                    Log("Log_Check_Matchmaking_Hanged");
                    _game.ClickCancel();
                    return true;
                }

                Log("Log_Matchmaking_Waiting", replace: [new("%ELAPSED%", mmTimer.Elapsed.ToString())]);
                for (int i = 0; i < 10 && !_isStopped; i++)
                {
                    Thread.Sleep(5000);
                }
                _config.GetWindowPositions();
            }

            return PlayMatch();
        }

        private bool PlayMatch()
        {
            Log("Log_Match_Playing");
            var active = true;
            var alreadySnapped = false;
            _rand = new Random();

            Log("Log_Match_SnapRoll");
            var snapLimit = 0.465;
            var snapRoll = Math.Round(_rand.NextDouble(), 3);
            var shouldSnap = snapRoll <= snapLimit;
            Log("Log_Match_SnapRoll_Limit", true, replace: [new("%VALUE%", snapLimit.ToString())]);
            Log("Log_Match_SnapRoll_Result", true, replace: [new("%VALUE%", snapRoll.ToString())]);
            Log("Log_Match_SnapRoll_Snap", true, replace: [new("%VALUE%", shouldSnap ? "YES" : "NO")]);

            _matchTimer = new Stopwatch();
            _matchTimer.Start();

            var currentTurn = 0;

            while (active && _matchTimer.Elapsed.Minutes < 15)
            {
                _config.GetWindowPositions();

                Log("Log_Check_ActiveMatch", true);
                if (!Check(() => _game.CanIdentifyActiveLadderMatch()))
                {
                    var check = false;
                    for (int x = 1; x < 3 && !check; x++)
                    {
                        Log("Log_Check_ActiveMatch_Failed");
                        _config.GetWindowPositions();
                        _game.ResetClick();
                        check = Check(() => _game.CanIdentifyActiveLadderMatch());
                        Thread.Sleep(2500);
                    }

                    active = check;
                }
                else
                {
                    if (currentTurn++ >= _retreatAfterTurn)
                    {
                        Log("Log_Match_ReachedTurnLimit", replace: [new("%VALUE%", _retreatAfterTurn.ToString())]);
                        _game.ClickRetreat();
                        for (int i = 0; i < 10 && !_isStopped; i++)
                        {
                            Thread.Sleep(5000);
                        }
                    }
                    else
                    {
                        Log("Log_Match_PlayingCards", replace: [new("%VALUE%", currentTurn.ToString())]);
                        _game.PlayHand();
                        for (int i = 0; i < 10 && !_isStopped; i++)
                        {
                            Thread.Sleep(1000);
                        }

                        _config.GetWindowPositions();

                        Log("Log_Check_EnergyState", true);
                        if (!Check(_game.CanIdentifyZeroEnergy))
                        {
                            Log("Log_Match_LeftoverEnergy");
                            _game.PlayHand();
                        }

                        Log("Log_Match_EndTurn");
                        _game.ClickNext();
                        for (int i = 0; i < 10 && !_isStopped; i++)
                        {
                            Thread.Sleep(1000);
                        }

                        _config.GetWindowPositions();

                        Log("Log_Check_TurnState", true);
                        while (Check(() => _game.CanIdentifyMidTurn()))
                        {
                            Log("Log_Match_WaitingForTurn");
                            for (int i = 0; i < 10 && !_isStopped; i++)
                            {
                                Thread.Sleep(4000);
                            }
                            _config.GetWindowPositions();
                        }
                    }
                }

                if (shouldSnap && !alreadySnapped)
                {
                    Log("Log_Match_Snapping");
                    _game.ClickSnap();
                    alreadySnapped = true;
                }
            }

            _config.GetWindowPositions();

            if (_matchTimer.Elapsed.Minutes > 15 && Check(() => _game.CanIdentifyLadderRetreatBtn()))
            {
                Log("Log_Match_MaxTimeReached");
                _game.ClickRetreat();
                for (int i = 0; i < 10 && !_isStopped; i++)
                {
                    Thread.Sleep(5000);
                }
            }

            Log("Log_Check_MatchEnd", true);
            if (Check(() => _game.CanIdentifyLadderMatchEnd()))
                return ExitMatch();

            return false;
        }

        private bool ExitMatch()
        {
            Log("Log_Match_Exiting");
            _config.GetWindowPositions();

            Log("Log_Check_PostMatchScreen", true);
            while (Check(() => _game.CanIdentifyLadderCollectRewardsBtn()) || Check(() => _game.CanIdentifyLadderMatchEndNextBtn()))
            {
                _game.ClickNext();
                for (int i = 0; i < 10 && !_isStopped; i++)
                {
                    Thread.Sleep(6000);
                }
                _config.GetWindowPositions();
            }

            return true;
        }

        protected override async Task ExecuteCycleAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && !_isStopped) // 同时检查停止标记
                {
                    // 检查停止（必须放在循环顶部）
                    CheckForStop();

                    // 检查暂停状态（通过异步方法）
                    await CheckPauseStateAsync(token);

                    // 执行具体逻辑（改为异步调用）
                    await Task.Run(async () =>
                    {
                        await RunAsync(token); // 将 Run() 改为异步方法，内部支持取消
                    }, token);

                    // 更频繁的延迟以提高响应速度
                    await Task.Delay(100, token);
                }
            }
            catch (OperationCanceledException)
            {
                Log("Operation canceled gracefully.");
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
            }
        }
    }
}