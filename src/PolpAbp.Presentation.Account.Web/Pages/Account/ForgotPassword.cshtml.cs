using Microsoft.AspNetCore.Mvc;
using PolpAbp.Framework.Emailing.Account;
using PolpAbp.Framework.Mvc.Interceptors;
using System.ComponentModel.DataAnnotations;
using Volo.Abp;
using Volo.Abp.Identity;
using Volo.Abp.Validation;

namespace PolpAbp.Presentation.Account.Web.Pages.Account
{
    [UnauthenticatedUser]
    [CurrentTenantRequired]
    public class ForgotPasswordModel : LoginModelBase
    {
        [BindProperty]
        public InputModel Input { get; set; }

        protected readonly IEmailingInterceptor EmailingInterceptor;
        protected readonly IFrameworkAccountEmailer FrameworkAccountEmailer;

        public ForgotPasswordModel(
            IEmailingInterceptor emailingInterceptor,
            IFrameworkAccountEmailer frameworkAccountEmailer) : base()
        {
            EmailingInterceptor= emailingInterceptor;
            FrameworkAccountEmailer = frameworkAccountEmailer;

            Input = new InputModel();
        }

        public virtual async Task<IActionResult> OnGetAsync()
        {
            // Load settings
            await LoadSettingsAsync();

            // Fall back to the identifiers handed over by the previous page,
            // which are not carried in the URL.
            ConsumeIdentifierHandoff();

            if (!string.IsNullOrEmpty(NormalizedEmailAddress))
            {
                Input.EmailAddress = NormalizedEmailAddress;
            }

            return Page();
        }

        public virtual async Task<IActionResult> OnPostAsync(string action)
        {
            // Load settings
            await LoadSettingsAsync();

            if (action == "Input")
            {
                try
                {
                    ValidateModel();

                    var user = await UserManager.FindByEmailAsync(Input.EmailAddress);

                    if (user != null)
                    {
                        // todo: Should we use the background ??
                        // In that case, the email may not be sent instantly.
                        var cc = await EmailingInterceptor.GetForgotPasswordEmailCcAsync(user.Id);
                        var resetToken = await UserManager.GeneratePasswordResetTokenAsync(user);
                        await FrameworkAccountEmailer.SendPasswordResetLinkWithCcAsync(
                            user.TenantId!.Value,
                            user.Id,
                            user.Email,
                            resetToken,
                            "MVC", // appname
                            cc: cc,
                            returnUrl: ReturnUrl,
                            returnUrlHash: ReturnUrlHash);

                        // For security reason, we will redirect the user to another page regardless 
                        // whether the user exits or not.
                        return RedirectToPage("/Account/PasswordResetLinkSent");
                    }
                    else
                    {
                        Alerts.Danger("We couldn't find an account associated with that email address. Please double-check the email you entered and try again.");
                    }
                }
                catch (UserFriendlyException e)
                {
                    Alerts.Danger(GetLocalizeExceptionMessage(e));
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

        public class InputModel
        {
            [Required]
            [DynamicStringLength(typeof(IdentityUserConsts), nameof(IdentityUserConsts.MaxEmailLength))]
            public string? EmailAddress { get; set; }
        }
    }
}
