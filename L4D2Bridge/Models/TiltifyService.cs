using System;
using System.Threading;
using System.Threading.Tasks;
using Tiltify;
using Tiltify.Exceptions;
using Tiltify.Models;
using L4D2Bridge.Types;

namespace L4D2Bridge.Models
{
    public class OnAuthUpdateArgs(string oAuthToken, string refreshToken)
    {
        public string OAuthToken = oAuthToken;
        public string RefreshToken = refreshToken;
    }

    public class TiltifyService : BaseService
    {
        private readonly Tiltify.Tiltify? Campaign;
        private readonly string CampaignId = string.Empty;
        private DateTime LastPolled;
        private Task? Runner;
        private readonly int PollInterval;
        private bool ShouldRun = true;

        // Fires whenever the authorization updated for Tiltify
        public Action<OnAuthUpdateArgs>? OnAuthUpdate { private get; set; }

        public TiltifyService(TiltifySettings config)
        {
            LastPolled = DateTime.UtcNow;
            ApiSettings apiSettings = new ApiSettings
            {
                ClientID = config.ClientID,
                ClientSecret = config.ClientSecret
            };

            Campaign = new Tiltify.Tiltify(null, null, apiSettings);
            CampaignId = config.CampaignID;
            PollInterval = config.PollingInterval;
        }
        ~TiltifyService()
        {
            ShouldRun = false;
        }

        public override string GetWorkflow() => "tiltify";
        public override ConsoleSources GetSource() => ConsoleSources.Tiltify;

        public override async void Start()
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
            while (ShouldRun)
            {
                if (Campaign == null || OnAuthUpdate == null)
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
                            {
                                PrintMessage($"One donation was invalid from {donoInfo.Name}, no amount tied to info.");
                                continue;
                            }

                            if (Double.TryParse(donoInfo.Amount.Value, out double DonationAmount))
                            {
                                Invoke(new SourceEvent(SourceEventType.Donation)
                                {
                                    Amount = DonationAmount,
                                    Currency = donoInfo.Amount.Currency,
                                    Name = donoInfo.Name,
                                    Message = donoInfo.Comment
                                });
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
    }
}