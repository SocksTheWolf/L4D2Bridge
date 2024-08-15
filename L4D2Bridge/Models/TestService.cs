﻿using L4D2Bridge.Types;
using L4D2Bridge.Utils;
using System;
using System.Threading.Tasks;

namespace L4D2Bridge.Models
{
    public class TestService : BaseServiceTickable
    {
        private TestSettings Settings;
        private Random rng = new();
        private bool Paused = false;
        public override ConsoleSources GetSource() => ConsoleSources.Test;
        public override string GetWorkflow() => Settings.WorkflowName;

        public TestService(TestSettings InSettings)
        {
            Settings = InSettings;
        }

        public void PauseExecution(bool ShouldPause)
        {
            Paused = ShouldPause;
            PrintMessage($"Test Service set paused to: {Paused}");
        }

        public void TogglePause()
        {
            PauseExecution(!Paused);
        }

        private void PrintMessageIfNotPaused(string message)
        {
            if (!Paused)
                PrintMessage(message);
        }

        protected override async Task Tick()
        {
            PrintMessageIfNotPaused("Starting test simulation in 1 minute...");
            await Task.Delay(60000);
            PrintMessageIfNotPaused("Simulation starting...");
            await Task.Delay(10000);
            PrintMessageIfNotPaused("Simulation started.");
            while (ShouldRun)
            {
                if (!Paused)
                {
                    // Max donation amount range is in $100
                    double Amount = Math.Round(rng.NextDouble() * 100.00, 2, MidpointRounding.ToZero);

                    // Execute on coin flip
                    if (rng.NextBool())
                    {
                        PrintMessage($"Test System generated ${Amount}!");
                        Invoke(new SourceEvent(SourceEventType.Donation)
                        {
                            Amount = Amount,
                            Name = "Director"
                        });
                    }
                }

                // Wait 10s-1min to try to execute again.
                await Task.Delay(rng.Next(10000, Settings.MaxMinutesToWait*60000 + 1));
            }
        }
    }
}
