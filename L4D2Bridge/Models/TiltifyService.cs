using System;
using System.Threading;
using System.Threading.Tasks;
using Tiltify;
using Tiltify.Exceptions;
using Tiltify.Models;
using L4D2Bridge.Types;

namespace L4D2Bridge.Models
{
    public class TiltifyService : BaseServiceTickable
    {
        private readonly Tiltify.Tiltify? Campaign;
        private TiltifySettings settings;
        private DateTime LastPolled;
        private bool HasLogin = false;
        private int LoginAttempts = 0;

        public TiltifyService(TiltifySettings config)
        {
            settings = config;
            LastPolled = DateTime.UtcNow;
            ApiSettings apiSettings = new ApiSettings
            {
                ClientID = config.ClientID,
                ClientSecret = config.ClientSecret
            };

            Campaign = new Tiltify.Tiltify(null, null, apiSettings);
        }

        public override string GetWorkflow() => "tiltify";
        public override ConsoleSources GetSource() => ConsoleSources.Tiltify;

        protected override bool Internal_Start()
        {
            if (!settings.IsValid())
            {
                PrintMessage("Tiltify settings are invalid, please fix and restart.");
                return false;
            }
            return base.Internal_Start();
        }

        private async Task<bool> Login()
        {
            ++LoginAttempts;
            if (Campaign == null)
                return false;

            try
            {
                AuthorizationResponse resp = await Campaign.Auth.Authorize();
                if (resp != null)
                {
                    string refreshToken = "";
                    if (!string.IsNullOrEmpty(resp.RefreshToken))
                        refreshToken = resp.RefreshToken;

                    // Clear login attempts on login success
                    LoginAttempts = 0;
                    HasLogin = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                PrintMessage($"Login hit exception: {ex}");
            }

            return false;
        }

        protected override async Task Tick()
        {
            using PeriodicTimer timer = new(TimeSpan.FromSeconds(settings.PollingInterval));
            while (ShouldRun)
            {
                if (Campaign == null)
                {
                    await timer.WaitForNextTickAsync(default);
                    continue;
                }

                if (!HasLogin)
                {
                    PrintMessage("Logging into Tiltify...");
                    if (await Login())
                    {
                        PrintMessage("Tiltify Ready!");
                        continue;
                    }
                    // Exponential backoff up to 10 min
                    await Task.Delay(Math.Min(1000 * (int)Math.Pow(2, LoginAttempts) / 2, 600000));
                    continue;
                }

                DateTime dateTime = DateTime.UtcNow;
                try
                {
                    GetCampaignDonationsResponse resp;
                    if (!settings.IsTeamCampaign)
                        resp = await Campaign.Campaigns.GetCampaignDonations(settings.CampaignID, LastPolled, 100);
                    else
                        resp = await Campaign.TeamCampaigns.GetCampaignDonations(settings.CampaignID, LastPolled, 100);
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
                    PrintMessage("Fetching a new token from Tiltify..");
                    await Login();
                    continue;
                }
                catch (Exception ex)
                {
                    PrintMessage($"Loop hit exception: {ex}");
                }

                await timer.WaitForNextTickAsync(default);
            }
        }
    }
}