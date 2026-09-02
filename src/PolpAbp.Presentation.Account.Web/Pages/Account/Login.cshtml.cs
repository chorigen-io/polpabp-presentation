using Microsoft.AspNetCore.Mvc;
using PolpAbp.Framework.Extensions;
using PolpAbp.Framework.Mvc.Cookies;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Auditing;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Volo.Abp.Validation;

namespace PolpAbp.Presentation.Account.Web.Pages.Account
{
    [CurrentTenantRequired]
    [UnauthenticatedUser]
    [DisableAuditing]
    public class LoginModel : LoginModelBase
    {
        [BindProperty]
        public LoginInputModel Input { get; set; }

        public Guid? TenantId { get; set; }

        public LoginModel() : base()
        {
            Input = new LoginInputModel();
        }

        public virtual async Task<IActionResult> OnGetAsync()
        {
            // Load settings
            await LoadSettingsAsync();

            // Identifiers are read from this page's own request parameters only, never from
            // inside the caller-supplied returnUrl. A returnUrl carrying username= used to be
            // parsed here and pre-filled from, and then travelled on to the password page --
            // putting the identifier back into an address through the side door. A caller that
            // wants pre-fill passes ?username= to this page directly, which still works; the
            // returnUrl itself is carried onward untouched, never parsed and never rewritten.
            // See chorigen-identity#81.

            // Note that only one of the two values should be set.
            // UserName or EmailAddress
            // It it the caller which decide what to do.

            // TenantId
            TenantId = CurrentTenant.Id;

            if (!string.IsNullOrEmpty(NormalizedUserName))
            {
                Input.UserName = NormalizedUserName;
                Input.IsUsingUserName = true;
            }
            else if (!string.IsNullOrEmpty(NormalizedEmailAddress))
            {
                Input.EmailAddress = NormalizedEmailAddress;
                Input.IsUsingUserName = false;
            }
            else
            {
                Input.UserName = string.Empty;
                Input.EmailAddress = string.Empty;
                Input.IsUsingUserName = false;
            }

            return Page();
        }

        public virtual async Task<IActionResult> OnPostAsync(string action)
        {
            // Load settings
            await LoadSettingsAsync();
            // Tenant Id
            TenantId = CurrentTenant.Id;

            if (action == "Input")
            {

                try
                {
                    ValidateModel();

                    // Extra sanity check 
                    if (Input.IsUsingUserName)
                    {
                        if (string.IsNullOrEmpty(Input.UserName))
                        {
                            Alerts.Danger("We need your username to sign you in! Please enter your username and try again.");
                            return Page();
                        }
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(Input.EmailAddress))
                        {
                            Alerts.Danger("We need your email to sign you in! Please enter your email address and try again.");
                            return Page();
                        }
                    }

                    Input.EmailAddress = Input.EmailAddress.Trim();
                    Input.UserName  = Input.UserName.Trim();

                    IdentityUser? user = null;

                    if (Input.IsUsingUserName)
                    {
                        user = await UserManager.FindByNameAsync(Input.UserName);
                    }
                    else
                    {
                        user = await UserManager.FindByEmailAsync(Input.EmailAddress);
                    }

                    if (user != null)
                    {
                        if (!user.IsExternal)
                        {
                            return HandOffToPasswordPage(user);
                        }
                        else
                        {
                            // Same identifier-in-URL defect as above, deliberately left alone:
                            // the SSO pages in sso/azure and sso/google read these query values,
                            // so the fix has to land across three repositories at once and is
                            // folded into the separate SSO rework. See chorigen-identity#73.

                            // Figure out the provider name.
                            var providerName = user.GetProperty<string>(ExternalProperties.UserIdentity.SsoScheme);
                            if (!string.IsNullOrEmpty(providerName))
                            {
                                var ssoUrl = Configuration[$@"PolpAbp:ExternalLogin:{providerName}:LoginPage"];
                                if (!string.IsNullOrEmpty(ssoUrl))
                                {
                                    return RedirectToPage(ssoUrl, new
                                    {
                                        // todo: Maybe use Id
                                        UserName = user.UserName,
                                        EmailAddress = user.Email,
                                        returnUrl = ReturnUrl,
                                        returnUrlHash = ReturnUrlHash
                                    });
                                }
                            }

                            Alerts.Danger("Something went wrong. If you believe you entered the correct information, but are still having trouble, please Contact us and we'll be happy to help.");
                        }
                    }
                    else
                    {
                        if (Input.IsUsingUserName)
                        {
                            Alerts.Danger("We couldn't find an account associated with that username. Please double-check the username you entered and try again.");
                        }
                        else
                        {
                            Alerts.Danger("We couldn't find an account associated with that email address. Please double-check the email you entered and try again.");
                        }
                    }
                }
                catch (AbpValidationException ex)
                {
                    // Handle this error.
                    foreach (var a in ex.ValidationErrors)
                    {
                        Alerts.Danger(a.ErrorMessage);
                    }
                }
            }

            return Page();
        }

        /// <summary>
        /// Sends a visitor whose local account has been resolved on to the password page,
        /// carrying who they are and how they said so.
        /// </summary>
        /// <remarks>
        /// Extracted so the choice this hands over can be asserted. Inline in the handler above
        /// it could not be: that method resolves the user manager, which needs a container the
        /// test project does not have. Here a test constructs the page, sets the posted input,
        /// and reads back what would travel.
        /// </remarks>
        protected virtual IActionResult HandOffToPasswordPage(IdentityUser user)
        {
            // Out of band, so the identifiers never appear in the password page's URL. This is
            // the highest-traffic hop on the sign-in path: every password sign-in crosses it, and
            // a URL persists in browser history and in server and proxy access logs.
            //
            // The visitor's own choice travels with them. It used to be pinned false here, so the
            // password page named and looked up the account by e-mail address however the visitor
            // had identified themselves -- showing a username visitor an address they never
            // typed, and leaving an account with no address on file unable to sign in at all.
            // See chorigen-identity#81.
            CaptureIdentifierHandoff(user.UserName, user.Email, Input.IsUsingUserName);

            return RedirectToPage("./LocalLogin", new
            {
                returnUrl = ReturnUrl,
                returnUrlHash = ReturnUrlHash
            });
        }

        public class LoginInputModel
        {
            public bool IsUsingUserName { get; set; }

            [Required]
            [DynamicStringLength(typeof(IdentityUserConsts), nameof(IdentityUserConsts.MaxUserNameLength))]
            public string UserName { get; set; }

            [Required]
            [EmailAddress]
            [DynamicStringLength(typeof(IdentityUserConsts), nameof(IdentityUserConsts.MaxEmailLength))]
            public string EmailAddress { get; set; }

        }
    }
}
