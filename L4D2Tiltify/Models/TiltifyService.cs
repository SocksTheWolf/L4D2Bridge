using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tiltify;
using Tiltify.Models;

namespace L4D2Tiltify.Models
{
    public class OnDonationArgs
    {
        public double Amount;
        public string Currency;
        public string Name;
        public string Comment;

        public OnDonationArgs(string InName, double InAmount, string InCurrency, string InComment)
        {
            Amount = InAmount;
            Currency = InCurrency;
            Name = InName;
            Comment = InComment;
        }
    }

    public class TiltifyService
    {
        private Tiltify.Tiltify Campaign;
        private string CampaignId;
        private DateTime LastPolled;
        private Task? Runner;
        private int PollInterval;

        // Print something to the console service (All Services have something like this)
        public Action<string>? OnConsolePrint { private get; set; }

        // Fires whenever donations are received
        public Action<OnDonationArgs>? OnDonationReceived { private get; set; }

        public TiltifyService(ConfigData config)
        {
            if (!config.IsValid)
                return;

            LastPolled = DateTime.UtcNow;
            ApiSettings apiSettings = new ApiSettings();
            apiSettings.OAuthToken = config.TiltifyOAuthToken;

            Campaign = new Tiltify.Tiltify(null, null, apiSettings);
            CampaignId = config.TiltifyCampaignID;
            PollInterval = config.TiltifyPollingInterval;
        }

        public void Start()
        {
            Runner = Tick(TimeSpan.FromSeconds(PollInterval));
        }

        async Task Tick(TimeSpan interval)
        {
            PrintMessage("Tiltify Ready!");
            using PeriodicTimer timer = new(interval);
            double temp;
            while (true)
            {
                if (OnDonationReceived == null)
                {
                    await timer.WaitForNextTickAsync(default);
                    continue;
                }

                DateTime dateTime = DateTime.UtcNow;
                
                try
                {
                    GetCampaignDonationsResponse resp = await Campaign.Campaigns.GetCampaignDonations(CampaignId, LastPolled, 100);
                    if (resp.Data.Length > 0)
                    {
                        PrintMessage($"Got {resp.Data.Length} new donations!");
                        LastPolled = dateTime;
                        foreach (DonationInformation donoInfo in resp.Data)
                        {
                            if (donoInfo.Amount == null)
                                continue;

                            if (Double.TryParse(donoInfo.Amount.Value, out temp))
                            {
                                OnDonationReceived.Invoke(new OnDonationArgs(donoInfo.Name, temp, donoInfo.Amount.Currency, donoInfo.Comment));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    PrintMessage(ex.ToString());
                }

                await timer.WaitForNextTickAsync(default);
            }
        }

        void PrintMessage(string message)
        {
            if (OnConsolePrint != null)
                OnConsolePrint.Invoke(message);
        }
    }
}