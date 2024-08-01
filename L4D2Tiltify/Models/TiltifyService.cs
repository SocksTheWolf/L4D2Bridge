using System;
using System.Threading;
using System.Threading.Tasks;
using Tiltify;
using Tiltify.Exceptions;
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

    public class OnAuthUpdateArgs
    {
        public string OAuthToken;
        public string RefreshToken;

        public OnAuthUpdateArgs(string oAuthToken, string refreshToken)
        {
            OAuthToken = oAuthToken;
            RefreshToken = refreshToken;
        }
    }

    public class TiltifyService
    {
        private Tiltify.Tiltify? Campaign;
        private string CampaignId = string.Empty;
        private DateTime LastPolled;
        private Task? Runner;
        private int PollInterval;
        private bool ShouldRun = true;

        // Print something to the console service (All Services have something like this)
        public Action<string>? OnConsolePrint { private get; set; }

        // Fires whenever donations are received
        public Action<OnDonationArgs>? OnDonationReceived { private get; set; }

        // Fires whenever the authorization updated for Tiltify
        public Action<OnAuthUpdateArgs>? OnAuthUpdate { private get; set; }

        public TiltifyService(ConfigData config)
        {
            if (!config.IsValid)
                return;

            LastPolled = DateTime.UtcNow;
            ApiSettings apiSettings = new ApiSettings();
            apiSettings.ClientID = config.TiltifyClientID;
            apiSettings.ClientSecret = config.TiltifyClientSecret;

            Campaign = new Tiltify.Tiltify(null, null, apiSettings);
            CampaignId = config.TiltifyCampaignID;
            PollInterval = config.TiltifyPollingInterval;
        }
        ~TiltifyService()
        {
            ShouldRun = false;
        }

        public async void Start()
        {
            if (Campaign == null)
                return;

            await Login();
            Runner = Tick(TimeSpan.FromSeconds(PollInterval));
        }

        private async Task Login()
        {
            if (OnAuthUpdate == null || Campaign == null)
                return;

            try
            {
                AuthorizationResponse resp = await Campaign.Auth.Authorize();
                if (resp != null)
                {
                    string refreshToken = "";
                    if (!string.IsNullOrEmpty(resp.RefreshToken))
                        refreshToken = resp.RefreshToken;

                    OnAuthUpdate.Invoke(new OnAuthUpdateArgs(resp.AccessToken, refreshToken));
                }
            }
            catch (Exception ex)
            {
                PrintMessage(ex.ToString());
            }
        }

        private async Task Tick(TimeSpan interval)
        {
            PrintMessage("Tiltify Ready!");
            using PeriodicTimer timer = new(interval);
            double temp;
            while (ShouldRun)
            {
                if (Campaign == null || OnDonationReceived == null || OnAuthUpdate == null)
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
                catch (TokenExpiredException)
                {
                    // If the token expires, get a new one.
                    PrintMessage("Fetching a new token from Tiltify.."); ;
                    await Login();
                    continue;
                }
                catch (Exception ex)
                {
                    PrintMessage(ex.ToString());
                }

                await timer.WaitForNextTickAsync(default);
            }
        }

        private void PrintMessage(string message)
        {
            if (OnConsolePrint != null)
                OnConsolePrint.Invoke(message);
        }
    }
}