using System;
using L4D2Bridge.Types;
using System.Threading;
using System.Threading.Tasks;

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
            try
            {
                // Try to push this to a worker thread
                if (!ThreadPool.QueueUserWorkItem(Internal_Invoke, eventData))
                {
                    // If it could not be pushed, then run it on the current thread.
                    OnSourceEvent?.Invoke(eventData);
                }
            }
            catch (NotSupportedException ex)
            {
                PrintMessage($"C# decided to be really confusing and forget that the `false` value exists for a boolean: {ex}");
            }
        }

        // Internal invoker that uses a threadpool to execute functionality.
        private void Internal_Invoke(object? eventData)
        {
            if (eventData == null)
                return;

            SourceEvent sourceEvent = (SourceEvent)eventData;
            try
            {
                OnSourceEvent?.Invoke(sourceEvent);
            }
            catch (Exception ex)
            {
                PrintMessage($"Failed to handle Invoke for {GetSource()}: {ex}");
            }
        }

        // What this service should display as when it's shown in the console log.
        public abstract ConsoleSources GetSource();

        // Returns the RulesEngine workflow name for this class. If the class doesn't have one,
        // this is just an empty string.
        public virtual string GetWorkflow() => string.Empty;

        // The starting entry point to all services
        public abstract void Start();
    }

    // A base service class, however it also allows for async tick operations
    public abstract class BaseServiceTickable : BaseService
    {
        // If set to true, this will initialize ticks
        protected bool ShouldRun = true;
        protected Task? TickTask = null;

        ~BaseServiceTickable()
        {
            ShouldRun = false;
        }

        // Implement the base start functionality
        public override void Start()
        {
            StartTick();
        }

        protected void StartTick()
        {
            TickTask = Tick();
        }

        protected virtual async Task Tick()
        {
            // async members cannot be abstract, so we just put a small delay and return
            await Task.Delay(1);
            return;
        }
    }
}
