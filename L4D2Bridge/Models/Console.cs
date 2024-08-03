using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using L4D2Bridge.Types;

namespace L4D2Bridge.Models
{
    public class ConsoleMessage
    {
        private DateTime Date { get; set; }
        public ConsoleSources Source { get; set; }
        public string Message { get; set; }

        public ConsoleMessage(string inMessage, ConsoleSources inSource)
        {
            Source = inSource;
            Message = inMessage;
            Date = DateTime.Now;
        }

        public string GetTypeStr() => nameof(Source);

        public bool IsExpired()
        {
            TimeSpan diff = DateTime.Now - Date;
            if (Math.Abs(diff.Minutes) > 5)
                return true;

            return false;
        }
    }

    public static class ObservableCollectionExtensions
    {
        public static void RemoveAll<T>(this ObservableCollection<T> collection,
                                                           Func<T, bool> condition)
        {
            for (int i = collection.Count - 1; i >= 0; i--)
            {
                if (condition(collection[i]))
                {
                    collection.RemoveAt(i);
                }
            }
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
            ConsoleMessages = new ObservableCollection<ConsoleMessage>();
        }
        ~ConsoleService()
        {
            ShouldRun = false;
        }

        public void Start()
        {
            // Console attempts to cleanup every 30s
            Ticker = Tick(TimeSpan.FromSeconds(30));
        }

        public void AddMessage(string inMessage, ConsoleSources source = ConsoleSources.None)
        {
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

        public async Task Tick(TimeSpan interval)
        {
            using PeriodicTimer timer = new(interval);
            while (ShouldRun)
            {
                ConsoleMessages.RemoveAll(msg => msg.IsExpired());
                await timer.WaitForNextTickAsync(default);
            }
        }
    }
}
