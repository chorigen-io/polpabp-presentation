using Microsoft.AspNetCore.Mvc;
using PolpAbp.Framework.Authorization.Users.Events;
using PolpAbp.Framework.Mvc.Cookies;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Volo.Abp.Account;
using Volo.Abp.Auditing;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.Validation;

namespace PolpAbp.Presentation.Account.Web.Pages.Account
{
    [UnauthenticatedUser]
    public class ResetPasswordModel : PolpAbpAccountPageModel
    {
        [Required]
        [HiddenInput]
        [BindProperty(SupportsGet = true)]
        public Guid UserId { get; set; }

        [Required]
        [HiddenInput]
        [BindProperty(SupportsGet = true)]
        public string? ResetToken { get; set; }

        [Required]
        [BindProperty]
        [DataType(DataType.Password)]
        [DynamicStringLength(typeof(IdentityUserConsts), nameof(IdentityUserConsts.MaxPasswordLength))]
        [DisableAuditing]
        public string? Password { get; set; }

        [Required]
        [BindProperty]
        [DataType(DataType.Password)]
        [DynamicStringLength(typeof(IdentityUserConsts), nameof(IdentityUserConsts.MaxPasswordLength))]
        [DisableAuditing]
        public string? ConfirmPassword { get; set; }

        public const string ResetPasswordTokenPurpose = "ResetPassword";

        public bool IsInvalidLink { get; set; }

        private readonly ILocalEventBus _localEventBus;
        private readonly IAppCookieManager _cookieManager;

        public ResetPasswordModel(ILocalEventBus localEventBus,
            IAppCookieManager appCookieManager) : base()
        {
            _localEventBus = localEventBus;
            _cookieManager = appCookieManager;
        }


        public virtual async Task<IActionResult> OnGetAsync()
        {
            await LoadSettingsAsync();

            // Find out the user 
            var user = await FindByIdBeyondTenantAsync(UserId);
            if (user == null)
            {
                Alerts.Danger("We couldn't find an account associated with the link. Please double-check your inbox for the password reset link.");
            }

            var verified = await UserManager.VerifyUserTokenAsync(user, UserManager.Options.Tokens.PasswordResetTokenProvider, 
                ResetPasswordTokenPurpose, ResetToken);
            if (!verified)
            {
                IsInvalidLink = true;
            }

            _cookieManager.SetTenantCookieValue(Response, user.TenantId!.Value.ToString());

            return Page();
        }

        public virtual async Task<IActionResult> OnPostAsync()
        {
            await LoadSettingsAsync();

            try
            {
                ValidateModel();

                await AccountAppService.ResetPasswordAsync(
                    new ResetPasswordDto
                    {
                        UserId = UserId,
                        ResetToken = ResetToken,
                        Password = Password
                    }
                );

                await _localEventBus.PublishAsync(new PasswordChangedEvent
                {
                    TenantId = CurrentTenant.Id,
                    UserId = UserId,
                    OperatorId = UserId,
                    NewPassword = Password
                });
            }
            catch (AbpIdentityResultException e)
            {
                if (!string.IsNullOrWhiteSpace(e.Message))
                {
                    Alerts.Warning(GetLocalizeExceptionMessage(e));
                    return Page();
                }

                throw;
            }
            catch (AbpValidationException ex)
            {
                // Handle this error.
                foreach (var a in ex.ValidationErrors)
                {
                    Alerts.Add(Volo.Abp.AspNetCore.Mvc.UI.Alerts.AlertType.Danger, a.ErrorMessage);
                }
                return Page();
            }

            //TODO: Try to automatically login!
            return RedirectToPage("./ResetPasswordSuccess");
        }

        protected override void ValidateModel()
        {
            var passwordValidator = new PasswordValidator(PwdComplexity, L, ModelState, nameof(Password), nameof(ConfirmPassword));
            if (passwordValidator.ValidateComplexity(Password))
            {
                passwordValidator.ValidateConfirmPassword(Password, ConfirmPassword);
            }

            base.ValidateModel();
        }

        protected override async Task LoadSettingsAsync()
        {
            await base.LoadSettingsAsync();
            await ReadInPasswordComplexityAsync();
        }
    }
}
