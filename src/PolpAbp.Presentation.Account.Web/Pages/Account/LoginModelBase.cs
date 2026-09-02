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

        // The password page re-stashes on every render so that a reload keeps the visitor's
        // place. That is a person sitting on a page, not a redirect hop, so it gets its own
        // longer allowance; widening HandoffLifetime instead would silently widen the
        // recovery-link hop above, which this change must leave alone.
        protected static readonly TimeSpan ReloadHandoffLifetime = TimeSpan.FromMinutes(5);

        [BindProperty(SupportsGet = true)]
        public string? UserName { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? EmailAddress { get; set; }

        private bool _isUsingUserName;

        /// <summary>
        /// Whether the visitor identified themselves by username rather than by e-mail address.
        /// </summary>
        /// <remarks>
        /// The setter records that a value arrived, because for a <see cref="bool"/> "supplied as
        /// false" and "never supplied at all" are otherwise indistinguishable — and the hand-off's
        /// precedence rule, that a value already on the request wins over a stashed one, has to
        /// tell them apart. Model binding calls this setter only when the request actually carried
        /// the value, which is exactly the signal needed.
        /// </remarks>
        [BindProperty(SupportsGet = true)]
        public bool IsUsingUserName
        {
            get => _isUsingUserName;
            set
            {
                _isUsingUserName = value;
                IsUsingUserNameSupplied = true;
            }
        }

        /// <summary>
        /// Whether <see cref="IsUsingUserName"/> carries a value this request supplied, as opposed
        /// to the default it would hold either way.
        /// </summary>
        protected bool IsUsingUserNameSupplied { get; private set; }

        public string NormalizedUserName => UserName ?? string.Empty;

        public string NormalizedEmailAddress => EmailAddress ?? string.Empty;

        /// <summary>
        /// Whether this request knows who is signing in. Pages that no longer carry the
        /// identifiers in their URL cannot rely on an authorization filter for this — the
        /// answer is only available after model binding and the hand-off have both run.
        /// </summary>
        /// <remarks>
        /// Keyed off the identifier actually selected, not off either one being present. A
        /// request that names one identifier kind while supplying the other used to pass this
        /// guard, render a password form with a blank account name, and then discard whatever
        /// was typed into it with the reason lost across a redirect. See chorigen-identity#91.
        /// </remarks>
        protected bool HasIdentifierContext => !string.IsNullOrEmpty(SelectedIdentifier);

        /// <summary>
        /// The identifier this sign-in is about: whichever one the visitor chose to identify
        /// themselves with. Pages name the account by this and look it up by this, so the two
        /// can never disagree.
        /// </summary>
        public string SelectedIdentifier => IsUsingUserName ? NormalizedUserName : NormalizedEmailAddress;

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
        /// Records who is signing in and how they said so, then hands it to the next request.
        /// </summary>
        /// <param name="userName">The account's canonical username.</param>
        /// <param name="emailAddress">The account's e-mail address, which may be empty.</param>
        /// <param name="isUsingUserName">Which of the two the visitor actually used.</param>
        /// <remarks>
        /// Takes the two identifiers as plain strings rather than the account record, so that a
        /// test can express an account with no e-mail address — a state the account model forbids
        /// at construction and whose setter is out of reach from the test assembly, yet one that
        /// reaches this code from stored data and, until this was fixed, blocked sign-in outright.
        /// </remarks>
        protected virtual void CaptureIdentifierHandoff(string? userName, string? emailAddress, bool isUsingUserName)
        {
            UserName = userName;
            EmailAddress = emailAddress;
            IsUsingUserName = isUsingUserName;

            StashIdentifierHandoff();
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
        /// <param name="lifetime">
        /// How old a stash may be and still be honored. Defaults to <see cref="HandoffLifetime"/>,
        /// which sizes a single redirect hop. A page that re-stashes on every render to survive a
        /// reload passes its own, longer allowance instead.
        /// </param>
        protected virtual void ConsumeIdentifierHandoff(TimeSpan? lifetime = null)
        {
            var allowance = lifetime ?? HandoffLifetime;

            // Read every key, even when the query string wins, so that nothing
            // stashed here survives into a later request.
            var handoffUserName = TempData[HandoffUserNameKey] as string;
            var handoffEmailAddress = TempData[HandoffEmailAddressKey] as string;
            var handoffIsUsingUserName = TempData[HandoffIsUsingUserNameKey] as string;
            var handoffStamp = TempData[HandoffStampKey] as string;

            // A stash is only honored for the immediate hop; an old one (an abandoned
            // hand-off resurfacing in a later visit or another tab) is discarded.
            if (!long.TryParse(handoffStamp, out var stampTicks)
                || DateTime.UtcNow - new DateTime(stampTicks, DateTimeKind.Utc) > allowance)
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

            // Assigns the field rather than the property: this is the fallback filling a gap the
            // request left, not a value the request supplied, and marking it as supplied would
            // misreport it to anything that asks later.
            if (!IsUsingUserNameSupplied && bool.TryParse(handoffIsUsingUserName, out var isUsingUserName))
            {
                _isUsingUserName = isUsingUserName;
            }
        }

    }
}
