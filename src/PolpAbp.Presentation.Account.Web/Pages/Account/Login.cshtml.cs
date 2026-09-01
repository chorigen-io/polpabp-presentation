using Microsoft.AspNetCore.Mvc;
using PolpAbp.Framework.Extensions;
using PolpAbp.Framework.Mvc.Cookies;
using System.ComponentModel.DataAnnotations;
using System.Web;
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

            // Flatten the ReturnURL
            var innerQueryParams = GetReturnUrlQueryParameters(ReturnUrl);
            if (innerQueryParams.ContainsKey("username"))
            {
                UserName = innerQueryParams["username"];
            }

            if (innerQueryParams.ContainsKey("emailaddress"))
            {
                EmailAddress = innerQueryParams["emailaddress"];
            }

            if (innerQueryParams.ContainsKey("isusingusername"))
            {
                IsUsingUserName = Boolean.Parse(innerQueryParams["isusingusername"]);
            }

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
                            // Hand the identifiers over out of band, so that they never appear
                            // in the password page's URL. This is the highest-traffic hop on the
                            // sign-in path: every password sign-in crosses it, and a URL persists
                            // in browser history and in server and proxy access logs.
                            UserName = user.UserName;
                            EmailAddress = user.Email;

                            // The redirect this replaces never carried IsUsingUserName, so the
                            // password page has always resolved and displayed the e-mail address
                            // whichever identifier the visitor typed. Forcing it false keeps that
                            // exactly as it was: this change moves the identifiers out of the URL
                            // and changes nothing about which account is looked up.
                            IsUsingUserName = false;

                            StashIdentifierHandoff();

                            return RedirectToPage("./LocalLogin", new
                            {
                                returnUrl = ReturnUrl,
                                returnUrlHash = ReturnUrlHash
                            });
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


        public static Dictionary<string, string> GetReturnUrlQueryParameters(string returnUrl)
        {
            if (string.IsNullOrEmpty(returnUrl))
            {
                return new Dictionary<string, string>();
            }

            // Split the returnUrl at the '?' character to separate the base URL and query string
            var parts = returnUrl.Split('?');
            if (parts.Length < 2)
            {
                return new Dictionary<string, string>();
            }

            // Extract the query string
            var queryString = parts[1];

            // Parse the query string into key-value pairs
            var queryDictionary = new Dictionary<string, string>();
            foreach (var keyValuePair in queryString.Split('&'))
            {
                var pair = keyValuePair.Split('=');
                if (pair.Length == 2)
                {
                    var decodedKey = Uri.UnescapeDataString(pair[0]).ToLower();
                    var decodedValue = DecodeStringFully(pair[1]);
                    queryDictionary.Add(decodedKey, decodedValue);
                }
            }

            return queryDictionary;
        }

        public static string DecodeStringFully(string encodedString)
        {
            if (string.IsNullOrEmpty(encodedString))
            {
                return encodedString;
            }

            string decodedString = encodedString;
            string previousDecodedString;

            do
            {
                previousDecodedString = decodedString;
                decodedString = HttpUtility.UrlDecode(decodedString);
            } while (decodedString != previousDecodedString);

            return decodedString;
        }
    }
}
