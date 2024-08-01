using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;
using CoreRCON;

namespace L4D2Bridge.Models
{
    public class RCONService
    {
        // Print something to the console service (All Services have something like this)
        public Action<string>? OnConsolePrint { private get; set; }

        // Flags whenever the system is currently paused
        public Action<bool>? OnPauseStatus { private get; set; }

        // Data
        private RCON? Server;
        private Task? RunTask;
        private Task? CheckPauseTask;
        private bool ShouldRun = true;
        private ConcurrentQueue<L4D2CommandBase> CommandQueue = new ConcurrentQueue<L4D2CommandBase>();

        public RCONService(ConfigData config) 
        {
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

        public void Start()
        {
            RunTask = Tick();
            CheckPauseTask = CheckPause();
        }

        // This is public so commands can print to the console still.
        public void PushToConsole(string message)
        {
            if (OnConsolePrint != null)
                OnConsolePrint.Invoke(message);
        }

        public void AddNewCommand(L4D2CommandBase command)
        {
            CommandQueue.Enqueue(command);
        }

        private async Task Tick()
        {
            if (Server == null)
                return;

            bool isConnected = false;
            Server.OnDisconnected += () => { 
                isConnected = false;
                PushToConsole("RCON Disconnected");
            };

            while (ShouldRun)
            {
                if (!isConnected)
                {
                    await Server.ConnectAsync();
                    isConnected = true;
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
                        if (!ranCommand) {
                            command.Retry(this);
                        }

                        if (command.WasSuccessful() && command.GetCommandType() == ECommandType.CheckPause)
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
