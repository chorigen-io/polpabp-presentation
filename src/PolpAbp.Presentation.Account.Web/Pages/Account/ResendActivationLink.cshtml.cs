using Microsoft.AspNetCore.Mvc;
using PolpAbp.Framework.Emailing.Account;
using PolpAbp.Framework.Mvc.Interceptors;
using PolpAbp.Framework.Settings;
using System.ComponentModel.DataAnnotations;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Volo.Abp.Settings;
using Volo.Abp.Validation;

namespace PolpAbp.Presentation.Account.Web.Pages.Account
{
    [UnauthenticatedUser]
    [CurrentTenantRequired]
    public class ResendActivationLinkModel : LoginModelBase
    {
        [BindProperty]
        public InputModel Input { get; set; }

        protected readonly IFrameworkAccountEmailer AccountEmailer;
        protected readonly IEmailingInterceptor EmailingInterceptor;
        protected MemberRegistrationEnum RegistrationApprovalType = MemberRegistrationEnum.RequireEmailActivation;

        public ResendActivationLinkModel(
            IFrameworkAccountEmailer frameworkAccountEmailer,
            IEmailingInterceptor emailingInterceptor) : base()
        {
            AccountEmailer = frameworkAccountEmailer;
            EmailingInterceptor = emailingInterceptor;

            Input = new InputModel();
        }

        public virtual async Task<IActionResult> OnGetAsync()
        {
            // Load settings
            await LoadSettingsAsync();

            // Fall back to the identifiers handed over by the previous page,
            // which are not carried in the URL.
            ConsumeIdentifierHandoff();

            // Pre-fill whichever identifier the visitor typed, and only that one: after a
            // username sign-in the account's e-mail address is never put on the page.
            // See chorigen-identity#86.
            Input.IsUsingUserName = IsUsingUserName;
            if (!string.IsNullOrEmpty(SelectedIdentifier))
            {
                if (IsUsingUserName)
                {
                    Input.UserName = SelectedIdentifier;
                }
                else
                {
                    Input.EmailAddress = SelectedIdentifier;
                }
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
                    // Only the identifier the form carried is required.
                    ModelState.Remove(Input.IsUsingUserName
                        ? $"{nameof(Input)}.{nameof(InputModel.EmailAddress)}"
                        : $"{nameof(Input)}.{nameof(InputModel.UserName)}");

                    ValidateModel();

                    // Resolve the account by the identifier the visitor typed; the activation
                    // message goes to the address on file, which the visitor need not know.
                    var user = Input.IsUsingUserName
                        ? await UserManager.FindByNameAsync(Input.UserName!.Trim())
                        : await UserManager.FindByEmailAsync(Input.EmailAddress!);

                    if (user != null)
                    {
                        if (user.IsActive)
                        {
                            if (user.EmailConfirmed)
                            {
                                Alerts.Warning(L["Login:AccountAlreadyActive"]);
                            }
                            else
                            {
                                // An account with no address on file cannot receive the link, so
                                // the page answers as for an unknown identifier (chorigen-identity#86 review).
                                if (string.IsNullOrWhiteSpace(user.Email))
                                {
                                    return Page();
                                }

                                // Otherwise, send out the email confirmation link.
                                // Send it instantly, because the user is waiting for it.
                                var cc = await EmailingInterceptor.GetActivationLinkEmailCcAsync(user.Id);
                                await AccountEmailer.SendEmailActivationLinkAsync(user.Id, cc);
                            }
                        }
                        else
                        {
                            // Otherwise, the user is not active.

                            var deactivatedBy = user.GetProperty<Guid?>(Framework.Extensions.ExternalProperties.UserIdentity.DeactivatedBy);
                            if (deactivatedBy.HasValue)
                            {
                                Alerts.Warning("We're sorry, but this account appears to be deactivated. You'll need to contact your organization administrator for reactivation.");
                                return Page();
                            }

                            // Decide if we can send out the activation link or not.
                            if (RegistrationApprovalType == MemberRegistrationEnum.RequireAdminApprovel)
                            {
                                Alerts.Warning(L["Login:AdminApprovalPending"]);
                                return Page();
                            }

                            // An account with no address on file cannot receive the link, so the
                            // page answers as for an unknown identifier (chorigen-identity#86 review).
                            if (string.IsNullOrWhiteSpace(user.Email))
                            {
                                return Page();
                            }

                            // Send it instantly, because the user is waiting for it.
                            var cc = await EmailingInterceptor.GetActivationLinkEmailCcAsync(user.Id);
                            await AccountEmailer.SendEmailActivationLinkAsync(user.Id, cc);
                        }

                        return RedirectToPage("./ResendActivationLinkSuccess", new
                        {
                            returnUrl = ReturnUrl,
                            returnUrlHash = ReturnUrlHash
                        });
                    }
                }
                catch (AbpValidationException ex)
                {
                    // Handle this error.
                    foreach (var a in ex.ValidationErrors)
                    {
                        Alerts.Add(Volo.Abp.AspNetCore.Mvc.UI.Alerts.AlertType.Danger, a.ErrorMessage);
                    }
                }
                catch (UserFriendlyException e)
                {
                    Alerts.Danger(GetLocalizeExceptionMessage(e));
                }
            }

            // For security reason, we will redirect the user to another page regardless 
            // whether the user exits or not.

            return Page();
        }

        protected async override Task LoadSettingsAsync()
        {
            await base.LoadSettingsAsync();
            RegistrationApprovalType = (MemberRegistrationEnum)(await SettingProvider.GetAsync<int>(FrameworkSettings.Account.RegistrationApprovalType));
        }

        public class InputModel
        {
            /// <summary>Whether the visitor identifies the account by username rather than e-mail address.</summary>
            public bool IsUsingUserName { get; set; }

            [Required]
            [DynamicStringLength(typeof(IdentityUserConsts), nameof(IdentityUserConsts.MaxUserNameLength))]
            public string? UserName { get; set; }

            [Required]
            [EmailAddress]
            [DynamicStringLength(typeof(IdentityUserConsts), nameof(IdentityUserConsts.MaxEmailLength))]
            public string? EmailAddress { get; set; }
        }
    }
}
