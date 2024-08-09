using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CoreRCON;
using L4D2Bridge.Types;

namespace L4D2Bridge.Models
{
    public class RCONService : BaseService
    {
        // Flags whenever the system is currently paused
        public Action<bool>? OnPauseStatus { private get; set; }

        // Data
        private bool ShouldRun = true;
        private readonly int MaxTaskAttempts;
        private ConcurrentQueue<L4D2CommandBase> CommandQueue = new();

        // Internals
        private RCON? Server;
        private CancellationTokenSource cancelToken = new();

        // Tasks
        private Task? RunTask;
        private Task? CheckPauseTask;

        public RCONService(ServerSettings config)
        {
            MaxTaskAttempts = config.MaxCommandAttempts;

            // See if we're an IP Address.
            if (!IPAddress.TryParse(config.ServerIP, out IPAddress? addr))
            {
                // We are a hostname, so attempt to fetch the IP address from DNS
                IPAddress[] Output = Dns.GetHostAddresses(config.ServerIP);
                if (Output.Length > 0)
                    addr = Output[0];
                else
                    return;
            }

            // Somehow the address is still invalid, so stop.
            if (addr == null || addr == IPAddress.None)
            {
                PrintMessage("Could not resolve address to connect to!");
                return;
            }

            IPEndPoint endpoint = new(addr, config.ServerPort);
            Server = new RCON(endpoint, config.Password, autoConnect: false);
        }
        ~RCONService()
        {
            CancelAllRetryTasks();
            ShouldRun = false;
        }
        public override ConsoleSources GetSource() => ConsoleSources.RCON;

        public override void Start()
        {
            if (Server == null)
            {
                PrintMessage("RCON server configuration was invalid!");
                return;
            }

            RunTask = Tick();
            CheckPauseTask = CheckPause();
        }

        // This is public so commands can print to the console still.
        public void PushToConsole(string message) => PrintMessage(message);

        public void AddNewCommand(L4D2CommandBase command)
        {
            if (Server == null)
                return;

            CommandQueue.Enqueue(command);
        }

        public void AddNewCommands(List<L4D2CommandBase> commands)
        {
            if (Server == null)
                return;

            foreach (L4D2CommandBase command in commands)
                AddNewCommand(command);
        }

        public void AddNewAction(L4D2Action action, string SenderName)
        {
            if (Server == null)
                return;

            L4D2CommandBase? OutCommand = L4D2CommandBuilder.BuildCommand(action, SenderName);
            if (OutCommand != null)
                AddNewCommand(OutCommand);
        }

        public void AddNewActions(List<L4D2Action> actions, string SenderName) 
        {
            if (Server == null)
                return;

            foreach (L4D2Action action in actions)
                AddNewAction(action, SenderName);
        }

        public void PrintNumCommands()
        {
            PrintMessage($"Command Queue is holding {CommandQueue.Count} commands!");
        }

        public void Clear()
        {
            PrintMessage("Command Queue cancelled all current and pending tasks!");
            CommandQueue.Clear();
            CancelAllRetryTasks();
        }

        private void CancelAllRetryTasks()
        {
            // Cancel any retries in progress
            cancelToken.Cancel();
            cancelToken.Dispose();
            cancelToken = new();
        }

        private async Task Tick()
        {
            if (Server == null)
                return;

            bool isConnected = false, hasConnected = false;
            Server.OnDisconnected += () => { 
                isConnected = false;
            };

            while (ShouldRun)
            {
                if (!isConnected)
                {
                    if (hasConnected)
                        PushToConsole("RCON Disconnected");

                    await Server.ConnectAsync();
                    isConnected = true;
                    hasConnected = true;
                    PushToConsole("RCON Connected");
                    continue;
                }

                // Check to see if we have any commands in the queue to run
                if (!CommandQueue.IsEmpty)
                {
                    if (CommandQueue.TryDequeue(out L4D2CommandBase? command))
                    {
                        bool ranCommand = await command.Execute(this, Server);
                        if (!ranCommand && command.GetCommandType() != ServerCommands.CheckPause)
                        {
                            // Do not attempt commands longer than the maximum amount of attempts
                            if (command.GetAttemptCount() < MaxTaskAttempts)
                                command.Retry(this, cancelToken.Token);
                            else
                                PrintMessage($"{command} timed out after {command.GetAttemptCount()} attempts");
                        }

                        if (command.WasSuccessful() && command.GetCommandType() == ServerCommands.CheckPause)
                        {
                            OnPauseStatus?.Invoke(((CheckPauseCommand)command).IsPaused());
                        }
                    }
                    else
                    {
                        await Task.Delay(1000);
                        continue;
                    }
                }
                else
                    await Task.Delay(1000);
            }
        }

        private async Task CheckPause()
        {
            TimeSpan time = TimeSpan.FromSeconds(45);
            while (ShouldRun)
            {
                AddNewCommand(new CheckPauseCommand());
                await Task.Delay(time, default);
            }
            
        }
    }
}
