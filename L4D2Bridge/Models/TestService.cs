﻿using L4D2Bridge.Types;
using L4D2Bridge.Utils;
using System;
using System.Threading.Tasks;

namespace L4D2Bridge.Models
{
    public class TestService : BaseService
    {
        private TestSettings Settings;
        private Random rng = new();
        private bool ShouldRun = true;
        private Task? Runner;
        public override ConsoleSources GetSource() => ConsoleSources.Test;
        public override string GetWorkflow() => Settings.WorkflowName;

        public TestService(TestSettings InSettings)
        {
            Settings = InSettings;
        }

        ~TestService()
        {
            ShouldRun = false;
        }

        // The starting entry point to all services
        public override void Start()
        {
            Runner = RunSimulation();
            PrintMessage("Test Service now Starting");
        }

        private async Task RunSimulation()
        {
            await Task.Delay(10000);
            while (ShouldRun)
            {
                // Max donation amount range is in $100
                double Amount = Math.Round(rng.NextDouble() * 100.00, 2, MidpointRounding.ToZero);

                // Execute on coin flip
                if (rng.NextBool())
                {
                    PrintMessage($"Test System generated ${Amount}!");
                    Invoke(new SourceEvent(SourceEventType.Donation, "TestRig", Amount, string.Empty));
                }

                // Wait 10s-1min to try to execute again.
                await Task.Delay(rng.Next(10000, Settings.MaxMinutesToWait*60000 + 1));
            }
        }
    }
}
