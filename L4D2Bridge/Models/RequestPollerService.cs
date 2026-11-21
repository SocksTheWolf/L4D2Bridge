using L4D2Bridge.Types;
using L4D2Bridge.Utils;
using L4D2Bridge.Settings;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace L4D2Bridge.Models
{
  public class RequestPollerService : BaseServiceTickable
  {
    private readonly RequestPollSettings settings;
    internal readonly HttpClient client = new();

    public RequestPollerService(RequestPollSettings config)
    {
      settings = config;
      client.DefaultRequestHeaders.Accept.Clear();
      client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
      client.DefaultRequestHeaders.UserAgent.ParseAdd("L4D2Bridge Client");
    }

    public override string GetWorkflow() => "donation";
    public override ConsoleSources GetSource() => ConsoleSources.RequestPoller;

    protected override async Task Tick()
    {
      using PeriodicTimer timer = new(TimeSpan.FromSeconds(settings.PollingInterval));
      while (ShouldRun)
      {
        try
        {
          string req = await client.GetStringAsync(settings.RequestLocation);
          var data = JsonConvert.DeserializeObject<PollRequestType>(req);
          if (data != null && data.results.Length > 0)
          {
            PrintMessage($"Got {data.results.Length} new donations!");
            foreach (PollRequestDataType donoInfo in data.results)
            {
              if (double.TryParse(donoInfo.amount, out double DonationAmount))
              {
                Invoke(new SourceEvent(SourceEventType.Donation)
                {
                  Amount = DonationAmount,
                  Currency = "USD",
                  Name = donoInfo.name
                });
              }
              else
              {
                PrintMessage($"Failed to parse {donoInfo.amount} into a double value");
              }
            }
          }
        }
        catch (Exception ex)
        {
          PrintMessage($"Poll Loop hit exception: {Helpers.PrintException(ex)}");
        }

        await timer.WaitForNextTickAsync(default);
      }
    }
  }
}
