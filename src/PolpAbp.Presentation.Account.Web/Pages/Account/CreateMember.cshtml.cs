using AspNetCore.ReCaptcha;
using Microsoft.AspNetCore.Mvc;
using PolpAbp.Framework.Exceptions.Identity;
using PolpAbp.Framework.Identity;
using PolpAbp.Framework.Identity.Dto;
using PolpAbp.Framework.Settings;
using PolpAbp.ZeroAdaptors.Authorization.Users;
using System.ComponentModel.DataAnnotations;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Identity;
using Volo.Abp.Settings;
using Volo.Abp.Validation;

namespace PolpAbp.Presentation.Account.Web.Pages.Account
{
    [UnauthenticatedUser]
    [CurrentTenantRequired]
    public class CreateMemberModel : PolpAbpAccountPageModel
    {
        [BindProperty]
        public PostInput Input { get; set; }

        protected readonly IMemberEnrollmentAppService _memberEnrollmentAppService;

        protected MemberRegistrationEnum RegistrationType = MemberRegistrationEnum.RequireEmailActivation;
        protected bool IsNewRegistrationNotyEnabled = false;

        protected readonly IReCaptchaService RecaptchaService;

        public CreateMemberModel(IReCaptchaService reCaptchaService,
            IMemberEnrollmentAppService memberEnrollmentAppService
            ) : base()
        {
            Input = new PostInput();

            RecaptchaService = reCaptchaService;
            _memberEnrollmentAppService = memberEnrollmentAppService;
        }


        public virtual async Task<IActionResult> OnGetAsync()
        {
            // Load settings
            await LoadSettingsAsync();

            return Page();
        }

        public virtual async Task<IActionResult> OnPostAsync(string action)
        {
            // Load settings
            await LoadSettingsAsync();

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

            if (action == "Input")
            {
                try
                {
                    // trim
                    Input.EmailAddress = Input!.EmailAddress!.Trim();
                    Input.Password = Input!.Password!.Trim();
                    Input.ConfirmPassword = Input!.ConfirmPassword!.Trim();

                    // Tenant not set this moment.
                    ValidateModel();

                    var createdRet = await _memberEnrollmentAppService.ProcessUserRegistrationAsync("MVC", new MemberEnrollmentInputDto
                    {
                        EmailAddress = Input.EmailAddress,
                        Password = Input.Password,
                        UserName = Input.EmailAddress,
                        FirstName = Input.FirstName,
                        LastName = Input.LastName
                    });

                    if (createdRet.ErrorCode == (int)UserOnboardingErrorEnum.EmailAlreadyUsed)
                    {
                        var errorDetail = IdentityErrorLikeInterpretor.Translate(UserOnboardingErrorEnum.EmailAlreadyUsed);

                        Alerts.Danger(errorDetail.Description);
                    }
                    else if (createdRet.ErrorCode == (int)UserOnboardingErrorEnum.UserNameUsed)
                    {
                        var errorDetail = IdentityErrorLikeInterpretor.Translate(UserOnboardingErrorEnum.UserNameUsed);

                        Alerts.Danger(errorDetail.Description);
                    }
                    else if (createdRet.ErrorCode == (int)UserOnboardingErrorEnum.PasswordComplexityNotEnough)
                    {
                        var errorDetail = IdentityErrorLikeInterpretor.Translate(UserOnboardingErrorEnum.PasswordComplexityNotEnough);

                        Alerts.Danger(errorDetail.Description);
                    }
                    else if (createdRet.ErrorCode == (int)UserOnboardingErrorEnum.MemberLicenseShortage)
                    {
                        var errorDetail = IdentityErrorLikeInterpretor.Translate(UserOnboardingErrorEnum.MemberLicenseShortage);

                        Alerts.Danger(errorDetail.Description);
                    }
                    else if (!createdRet.UserId.HasValue)
                    {
                        Alerts.Danger("Something went wrong. Please try it later.");
                    }
                    else
                    {
                        // Success 
                        if (createdRet.NextActionStatus != UserOnboardingNextActionEnum.WaitAdminApprovel && 
                            createdRet.NextActionStatus != UserOnboardingNextActionEnum.ActivateEmail)
                        {
                            var user = await UserManager.FindByIdAsync(createdRet.UserId!.Value.ToString());
                            await SignInManager.SignInAsync(user, true);
                        }

                        return RedirectToPage("./MemberRegisterSuccess", new
                        {
                            NextAction = (int)createdRet.NextActionStatus
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
                catch (UserFriendlyException ex)
                {
                    Alerts.Add(Volo.Abp.AspNetCore.Mvc.UI.Alerts.AlertType.Danger, ex.Message);
                }
            }

            return Page();
        }

        protected override void ValidateModel()
        {
            var passwordValidator = new PasswordValidator(PwdComplexity, L, ModelState, "Input.Password", "Input.ConfirmPassword");
            if (passwordValidator.ValidateComplexity(Input.Password))
            {
                passwordValidator.ValidateConfirmPassword(Input.Password, Input.ConfirmPassword);
            }

            base.ValidateModel();
        }

        protected override async Task LoadSettingsAsync()
        {
            await base.LoadSettingsAsync();
            await ReadInRecaptchaEnabledAsync();
            await ReadInPasswordComplexityAsync();

            // Load
            RegistrationType = (MemberRegistrationEnum)(await SettingProvider.GetAsync<int>(FrameworkSettings.Account.RegistrationApprovalType));
            IsNewRegistrationNotyEnabled = await SettingProvider.GetAsync<bool>(FrameworkSettings.Account.IsNewRegistrationNotyEnabled);

            // No need to read external auth 
        }

        protected override async Task ReadInRecaptchaEnabledAsync()
        {
            await base.ReadInRecaptchaEnabledAsync();
            if (IsRecaptchaEnabled)
            {
                IsRecaptchaEnabled = await SettingProvider.GetAsync<bool>(FrameworkSettings.Security.UseCaptchaOnRegistration);
            }
        }

        public class PostInput : IHasConfirmPassword
        {
            // Admin information below
            // todo: Some email address is not allowe?
            [Required]
            [EmailAddress]
            [DynamicStringLength(typeof(IdentityUserConsts), nameof(IdentityUserConsts.MaxEmailLength))]
            public string? EmailAddress { get; set; }

            [Required]
            [DynamicStringLength(typeof(IdentityUserConsts), nameof(IdentityUserConsts.MaxPasswordLength))]
            [DataType(DataType.Password)]
            [DisableAuditing]
            public string? Password { get; set; }

            [Required]
            [DynamicStringLength(typeof(IdentityUserConsts), nameof(IdentityUserConsts.MaxPasswordLength))]
            [DataType(DataType.Password)]
            [DisableAuditing]
            public string? ConfirmPassword { get; set; }

            [Required]
            [DynamicStringLength(typeof(IdentityUserConsts), nameof(IdentityUserConsts.MaxNameLength))]
            public string? FirstName { get; set; }

            [Required]
            [DynamicStringLength(typeof(IdentityUserConsts), nameof(IdentityUserConsts.MaxSurnameLength))]
            public string? LastName { get; set; }

        }
    }
}
