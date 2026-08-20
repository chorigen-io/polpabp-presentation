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
