using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using L4D2Bridge.Utils;
using L4D2Bridge.Types;

namespace L4D2Bridge.Models
{
    public class ConsoleMessage(string inMessage, ConsoleSources inSource)
    {
        private DateTime Date { get; set; } = DateTime.Now;
        public ConsoleSources Source { get; set; } = inSource;
        public string Message { get; set; } = inMessage;

        public string GetTypeStr() => nameof(Source);

        public bool IsExpired(int messageLifetime)
        {
            if (messageLifetime == 0)
                return false;

            TimeSpan diff = DateTime.Now - Date;
            if (Math.Abs(diff.Minutes) > messageLifetime)
                return true;

            return false;
        }
    }

    // This does not need to inherit from BaseService because it doesn't need a console printer
    // (as it is the console printer)
    public class ConsoleService
    {
        public ObservableCollection<ConsoleMessage> ConsoleMessages { get; private set; }
        private Task? Ticker;
        private bool ShouldRun = true;

        public ConsoleService()
        {
            ConsoleMessages = [];
        }
        ~ConsoleService()
        {
            ShouldRun = false;
        }

        public void Start(int maxMessageLifetime=5)
        {
            // Console attempts to cleanup every 30s
            Ticker = Tick(TimeSpan.FromSeconds(30), maxMessageLifetime);
        }

        public void AddMessage(string inMessage, ConsoleSources source = ConsoleSources.None)
        {
            // Don't bother adding messages that are blank
            if (string.IsNullOrWhiteSpace(inMessage))
                return;

            Dispatcher.UIThread.Post(() => ConsoleMessages.Add(new ConsoleMessage(inMessage, source)));
        }

        public void AddMessage(string inMessage, BaseService service)
        {
            AddMessage(inMessage, service.GetSource());
        }

        public void ClearAllMessages()
        {
            Dispatcher.UIThread.Post(() => ConsoleMessages.Clear());
        }

        public async Task Tick(TimeSpan interval, int maxMessageLifetime)
        {
            using PeriodicTimer timer = new(interval);
            while (ShouldRun)
            {
                ConsoleMessages.RemoveAll(msg => msg.IsExpired(maxMessageLifetime));
                await timer.WaitForNextTickAsync(default);
            }
        }
    }
}
