using System;
using L4D2Bridge.Types;

namespace L4D2Bridge.Models
{
    // A base class to every service, this allows for common functions to be used easily across various service providers
    public abstract class BaseService
    {
        // Print something to the console service (All Services have something like this)
        public Action<string>? OnConsolePrint { private get; set; }

        // Fires whenever the service has an event (such as donation received)
        public event SourceEventHandler? OnSourceEvent;

        // Helper function for printing messages to console (via Actions)
        protected void PrintMessage(string message)
        {
            OnConsolePrint?.Invoke(message);
        }

        // An invoker function that broadcasts to the event delegate that the service has
        // an event trigger.
        protected void Invoke(SourceEvent eventData)
        {
            OnSourceEvent?.Invoke(eventData);
        }

        public abstract ConsoleSources GetSource();

        // Returns the RulesEngine workflow name for this class. If the class doesn't have one,
        // this is just an empty string.
        public virtual string GetWorkflow() => string.Empty;

        // The starting entry point to all services
        public abstract void Start();
    }
}
