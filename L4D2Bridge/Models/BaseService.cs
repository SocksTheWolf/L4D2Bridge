using System;
using L4D2Bridge.Types;

namespace L4D2Bridge.Models
{
    // A base class to every service, this allows for common functions to be used easily across various service providers
    public abstract class BaseService
    {
        // Print something to the console service (All Services have something like this)
        public Action<string>? OnConsolePrint { private get; set; }

        // Fires whenever donations are received
        public Action<SourceEvent>? OnSourceEvent { protected get; set; }

        // Helper function for printing messages to console (via Actions)
        protected void PrintMessage(string message)
        {
            if (OnConsolePrint != null)
                OnConsolePrint.Invoke(message);
        }

        public abstract ConsoleSources GetSource();

        // Returns the RulesEngine workflow name for this class. If the class doesn't have one,
        // this is just an empty string.
        public virtual string GetWorkflow() => string.Empty;

        // The starting entry point to all services
        public abstract void Start();
    }
}
