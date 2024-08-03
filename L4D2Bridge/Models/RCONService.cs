using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
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
        private int MaxTaskAttempts;
        private ConcurrentQueue<L4D2CommandBase> CommandQueue = new ConcurrentQueue<L4D2CommandBase>();

        // Internals
        private RCON? Server;

        // Tasks
        private Task? RunTask;
        private Task? CheckPauseTask;

        public RCONService(ConfigData config)
        {
            MaxTaskAttempts = config.MaxTaskRetries;
            if (!config.IsValid)
                return;

            IPAddress? addr = IPAddress.None;
            // See if we're an IP Address.
            if (!IPAddress.TryParse(config.RConServerIP, out addr))
            {
                // We are a hostname, so attempt to fetch the IP address from DNS
                IPAddress[] Output = Dns.GetHostAddresses(config.RConServerIP);
                if (Output.Length > 0)
                    addr = Output[0];
                else
                    return;
            }

            // Somehow the address is still invalid, so stop.
            if (addr == null || addr == IPAddress.None)
                return;

            IPEndPoint endpoint = new IPEndPoint(addr, config.RConServerPort);
            Server = new RCON(endpoint, config.RConPassword, autoConnect: false);
        }
        ~RCONService()
        {
            ShouldRun = false;
        }
        public override ConsoleSources GetSource() => ConsoleSources.RCON;

        public override void Start()
        {
            RunTask = Tick();
            CheckPauseTask = CheckPause();
        }

        // This is public so commands can print to the console still.
        public void PushToConsole(string message)
        {
            PrintMessage(message);
        }

        public void AddNewCommand(L4D2CommandBase command)
        {
            CommandQueue.Enqueue(command);
        }

        public void AddNewCommands(List<L4D2CommandBase> commands)
        {
            foreach (L4D2CommandBase command in commands)
                AddNewCommand(command);
        }

        public void AddNewAction(L4D2Action action, string SenderName)
        {
            L4D2CommandBase? OutCommand = L4D2CommandBuilder.BuildCommand(action, SenderName);
            if (OutCommand != null)
                AddNewCommand(OutCommand);
        }

        public void AddNewActions(List<L4D2Action> actions, string SenderName) 
        { 
            foreach (L4D2Action action in actions)
                AddNewAction(action, SenderName);
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
                if (CommandQueue.Count > 0)
                {
                    L4D2CommandBase? command;
                    if (CommandQueue.TryDequeue(out command))
                    {
                        bool ranCommand = await command.Execute(this, Server);
                        if (!ranCommand && command.GetCommandType() != ServerCommands.CheckPause) {

                            // Do not attempt commands longer than the maximum amount of attempts
                            if (command.GetAttemptCount() < MaxTaskAttempts)
                                command.Retry(this);
                            else
                                PrintMessage($"{command} timed out after {command.GetAttemptCount()} attempts");
                        }

                        if (command.WasSuccessful() && command.GetCommandType() == ServerCommands.CheckPause)
                        {
                            if (OnPauseStatus != null)
                                OnPauseStatus.Invoke(((CheckPauseCommand)command).IsPaused());
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
            TimeSpan time = TimeSpan.FromMinutes(2);
            while (ShouldRun)
            {
                AddNewCommand(new CheckPauseCommand());
                await Task.Delay(time, default);
            }
            
        }
    }
}
