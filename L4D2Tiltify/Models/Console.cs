﻿using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace L4D2Tiltify.Models
{
    public enum EConsoleSource
    {
        None,
        Main,
        RCON,
        Tiltify
    }

    public class ConsoleMessage
    {
        private DateTime Date { get; set; }
        public EConsoleSource Source { get; set; }
        public string Message { get; set; }

        public ConsoleMessage(string inMessage, EConsoleSource inSource)
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

        public void Initialize(ConfigData config)
        {
            // Console attempts to cleanup every 30s
            Ticker = Tick(TimeSpan.FromSeconds(30));
        }

        public void AddMessage(string inMessage, EConsoleSource source = EConsoleSource.None)
        {
            ConsoleMessages.Add(new ConsoleMessage(inMessage, source));
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
