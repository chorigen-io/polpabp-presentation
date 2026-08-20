using AspNetCore.ReCaptcha;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PolpAbp.Framework.Mvc.Cookies;
using PolpAbp.Framework.Settings;
using System.ComponentModel.DataAnnotations;
using Volo.Abp;
using Volo.Abp.Account.Settings;
using Volo.Abp.Auditing;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Volo.Abp.Identity.AspNetCore;
using Volo.Abp.Settings;
using Volo.Abp.Validation;

namespace PolpAbp.Presentation.Account.Web.Pages.Account
{
    [CurrentTenantRequired]
    [UnauthenticatedUser]
    [UserNameOrEmailRequired]
    public class LocalLoginModel : LoginModelBase
    {
        [BindProperty]
        public PostInput Input { get; set; }

        public bool ShowActivationLink { get; set; }

        protected MemberRegistrationEnum RegistrationApprovalType = MemberRegistrationEnum.RequireEmailActivation;
        protected readonly IReCaptchaService RecaptchaService;

        public LocalLoginModel(IReCaptchaService recaptchaService) : base()
        {

            Input = new PostInput();
            RecaptchaService = recaptchaService;    
        }

        public virtual async Task<IActionResult> OnGetAsync()
        {
            // Load settings
            await LoadSettingsAsync();

            return Page();
        }

        public virtual async Task<IActionResult> OnPostAsync(string action)
        {
            if (action == "ForgotPassword" || action == "ResendActivation")
            {
                // Hand the identifiers over out of band, so that they never appear in the URL.
                // No settings are needed on this path.
                StashIdentifierHandoff();

                return RedirectToPage(action == "ForgotPassword" ? "./ForgotPassword" : "./ResendActivationLink");
            }

            // Load settings
            await LoadSettingsAsync();

            if (action == "Input")
            {
                try
                {
                    // On purpose use it here to allow the other actions.
                    await CheckLocalLoginAsync();

                    if (IsRecaptchaEnabled)
                    {
                        var recaptchaValue = ParseRecaptchResponse();
                        var isGood = await RecaptchaService.VerifyAsync(recaptchaValue);
                        if (!isGood)
                        {
                            // TODO: localization
                            Alerts.Danger("Please verify that you are not a robot.");

                            return Page();
                        }
                    }

                    ValidateModel();

                    IdentityUser? user = null;

                    if (IsUsingUserName)
                    {
                        user = await UserManager.FindByNameAsync(NormalizedUserName);
                        if (user == null)
                        {
                            Alerts.Danger("We couldn't find an account associated with that username. Please double-check the username you entered and try again.");
                            return RedirectToPage("./Login", new
                            {
                                returnUrl = ReturnUrl,
                                returnUrlHash = ReturnUrlHash
                            });
                        }
                    }
                    else
                    {
                        user = await UserManager.FindByEmailAsync(NormalizedEmailAddress);

                        if (user == null)
                        {
                            Alerts.Danger("We couldn't find an account associated with that email address. Please double-check the email you entered and try again.");
                            return RedirectToPage("./Login", new
                            {
                                returnUrl = ReturnUrl,
                                returnUrlHash = ReturnUrlHash
                            });
                        }
                    }

                    // Check if the user is allowed to login or not 
                    if (user.IsActive == false)
                    {
                        var deactivatedBy = user.GetProperty<Guid?>(Framework.Extensions.ExternalProperties.UserIdentity.DeactivatedBy);
                        if (deactivatedBy.HasValue)
                        {
                            Alerts.Warning("We're sorry, but this account appears to be deactivated. You'll need to contact your organization administrator for reactivation.");
                            return Page();
                        }
                        else if (RegistrationApprovalType == MemberRegistrationEnum.RequireEmailActivation)
                        {
                            ShowActivationLink = true;
                            return Page();
                        }
                        else if (RegistrationApprovalType == MemberRegistrationEnum.RequireAdminApprovel)
                        {
                            Alerts.Warning(L["Login:AdminApprovalPending"]);
                            return Page();
                        }
                    }

                    // todo: Login 
                    var result = await SignInManager.PasswordSignInAsync(
                        user!.UserName,
                        Input.Password,
                        Input.RememberMe,
                        true
                        );

                    // TODO. 

                    /*
                    await IdentitySecurityLogManager.SaveAsync(new IdentitySecurityLogContext()
                    {
                        Identity = IdentitySecurityLogIdentityConsts.Identity,
                        Action = result.ToIdentitySecurityLogAction(),
                        UserName = UserName
                    }); */

                    if (result.RequiresTwoFactor)
                    {
                        // todo: Check if wen directly redirect a user to
                        // the two factor sign in page or request page
                        // depending on if a user set up an authenticator.

                        return RedirectToPage("./TwoFactorRequest", new
                        {
                            RememberMe = Input.RememberMe,
                            returnUrl = ReturnUrl,
                            returnUrlHash = ReturnUrlHash
                        });
                    }

                    if (result.IsLockedOut)
                    {
                        Alerts.Warning(L["UserLockedOutMessage"]);
                        return Page();
                    }

                    if (result.IsNotAllowed)
                    {
                        Alerts.Warning(L["LoginIsNotAllowed"]);
                        return Page();
                    }

                    if (!result.Succeeded)
                    {
                        Alerts.Danger(L["InvalidUserNameOrPassword"]);
                        return Page();
                    }

                    // The point where we have to check if the user 
                    // should update his/her password.
                    if (user.ShouldChangePasswordOnNextLogin())
                    {
                        return RedirectToPage("./UpdatePassword", new
                        {
                            returnUrl = ReturnUrl,
                            returnUrlHash = ReturnUrlHash
                        });
                    }

                    var mainPage = Configuration["PolpAbp:Account:MainEntry"];
                    return RedirectToPage(mainPage, new
                    {
                        returnUrl = ReturnUrl,
                        returnUrlHash = ReturnUrlHash
                    });
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

        protected virtual async Task CheckLocalLoginAsync()
        {
            if (!await SettingProvider.IsTrueAsync(AccountSettingNames.EnableLocalLogin))
            {
                throw new UserFriendlyException(L["LocalLoginDisabledMessage"]);
            }
        }

        protected override async Task LoadSettingsAsync()
        {
            await base.LoadSettingsAsync();
            RegistrationApprovalType = (MemberRegistrationEnum)(await SettingProvider.GetAsync<int>(FrameworkSettings.Account.RegistrationApprovalType));

            Input.AccountId = IsUsingUserName ? NormalizedUserName : NormalizedEmailAddress;
        }

        public class PostInput
        {
            public string? AccountId { get; set; }

            [Required]
            [DynamicStringLength(typeof(IdentityUserConsts), nameof(IdentityUserConsts.MaxPasswordLength))]
            [DataType(DataType.Password)]
            [DisableAuditing]
            public string? Password { get; set; }

            public bool RememberMe { get; set; }
        }
    }
}
