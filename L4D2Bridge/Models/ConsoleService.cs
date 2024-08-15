using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using L4D2Bridge.Utils;
using L4D2Bridge.Types;
using Avalonia.Controls;
using System.Linq;

namespace L4D2Bridge.Models
{
    public class ConsoleMessage(string inMessage, ConsoleSources inSource)
    {
        private DateTime Date { get; set; } = DateTime.Now;
        public ConsoleSources Source { get; set; } = inSource;
        public string Message { get; set; } = inMessage;

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

    public class ConsoleService : BaseServiceTickable
    {
        public ObservableCollection<ConsoleMessage> ConsoleMessages { get; private set; }
        public static DataGrid? ConsoleHistory;
        private int MaxMessageLifetime = 5;

        public ConsoleService()
        {
            ConsoleMessages = [];
        }

        public override ConsoleSources GetSource() => ConsoleSources.None;

        public void SetMaxMessageLifetime(int value)
        {
            MaxMessageLifetime = value;
        }

        public void AddMessage(string inMessage, ConsoleSources source = ConsoleSources.None)
        {
            // Don't bother adding messages that are blank
            if (string.IsNullOrWhiteSpace(inMessage))
                return;

            Dispatcher.UIThread.Post(() => {
                ConsoleMessages.Add(new ConsoleMessage(inMessage, source));
                ConsoleHistory?.ScrollIntoView(ConsoleMessages.Last(), null);
            });
        }

        public void AddMessage(string inMessage, BaseService service)
        {
            AddMessage(inMessage, service.GetSource());
        }

        public void ClearAllMessages()
        {
            Dispatcher.UIThread.Post(() => ConsoleMessages.Clear());
        }

        protected override async Task Tick()
        {
            using PeriodicTimer timer = new(TimeSpan.FromSeconds(30));
            while (ShouldRun)
            {
                ConsoleMessages.RemoveAll(msg => msg.IsExpired(MaxMessageLifetime));
                await timer.WaitForNextTickAsync(default);
            }
        }
    }
}
