using System.Web;
using Microsoft.AspNetCore.Mvc;
using PolpAbp.Framework.Settings;
using Volo.Abp.Settings;

namespace PolpAbp.Presentation.Account.Web.Pages.Account
{
    public abstract class LoginModelBase : PolpAbpAccountPageModel
    {
        // Keys of the one-hop identifier hand-off, which lets one account page
        // pre-fill another without ever putting an identifier into a URL.
        protected const string HandoffUserNameKey = "PolpAbp.Account.Handoff.UserName";
        protected const string HandoffEmailAddressKey = "PolpAbp.Account.Handoff.EmailAddress";
        protected const string HandoffIsUsingUserNameKey = "PolpAbp.Account.Handoff.IsUsingUserName";
        protected const string HandoffStampKey = "PolpAbp.Account.Handoff.Stamp";

        // A stash that is never consumed (the redirect GET never lands) would otherwise
        // sit in the TempData cookie for the rest of the browser session.
        protected static readonly TimeSpan HandoffLifetime = TimeSpan.FromMinutes(2);

        [BindProperty(SupportsGet = true)]
        public string? UserName { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? EmailAddress { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool IsUsingUserName { get; set; }

        public string NormalizedUserName => UserName ?? string.Empty;

        public string NormalizedEmailAddress => EmailAddress ?? string.Empty;

        protected override async Task LoadSettingsAsync()
        {
            await base.LoadSettingsAsync();
            // Recaptcha 
            await ReadInRecaptchaEnabledAsync();
        }

        protected override async Task ReadInRecaptchaEnabledAsync()
        {
            await base.ReadInRecaptchaEnabledAsync();

            if (IsRecaptchaEnabled)
            {
                IsRecaptchaEnabled = await SettingProvider.GetAsync<bool>(FrameworkSettings.Security.UseCaptchaOnLogin);
            }
        }

        /// <summary>
        /// Stashes the current identifiers for the very next request, so that a
        /// hand-off to another account page carries nothing in the URL.
        /// </summary>
        protected virtual void StashIdentifierHandoff()
        {
            TempData[HandoffUserNameKey] = UserName;
            TempData[HandoffEmailAddressKey] = EmailAddress;
            TempData[HandoffIsUsingUserNameKey] = IsUsingUserName.ToString();
            TempData[HandoffStampKey] = DateTime.UtcNow.Ticks.ToString();
        }

        /// <summary>
        /// Consumes the identifiers stashed by the previous request. Values bound
        /// from the query string stay authoritative; the hand-off is the fallback.
        /// </summary>
        protected virtual void ConsumeIdentifierHandoff()
        {
            // Read every key, even when the query string wins, so that nothing
            // stashed here survives into a later request.
            var handoffUserName = TempData[HandoffUserNameKey] as string;
            var handoffEmailAddress = TempData[HandoffEmailAddressKey] as string;
            var handoffIsUsingUserName = TempData[HandoffIsUsingUserNameKey] as string;
            var handoffStamp = TempData[HandoffStampKey] as string;

            // A stash is only honored for the immediate hop; an old one (an abandoned
            // hand-off resurfacing in a later visit or another tab) is discarded.
            if (!long.TryParse(handoffStamp, out var stampTicks)
                || DateTime.UtcNow - new DateTime(stampTicks, DateTimeKind.Utc) > HandoffLifetime)
            {
                return;
            }

            if (string.IsNullOrEmpty(UserName) && !string.IsNullOrEmpty(handoffUserName))
            {
                UserName = handoffUserName;
            }

            if (string.IsNullOrEmpty(EmailAddress) && !string.IsNullOrEmpty(handoffEmailAddress))
            {
                EmailAddress = handoffEmailAddress;
            }

            if (!IsUsingUserName && bool.TryParse(handoffIsUsingUserName, out var isUsingUserName))
            {
                IsUsingUserName = isUsingUserName;
            }
        }

    }
}
