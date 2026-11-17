using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Auditing;
using Volo.Abp.Identity;
using Volo.Abp.Validation;

namespace PolpAbp.Presentation.Account.Web.Pages.Account
{
    [UnauthenticatedUser]
    [CurrentTenantEmpty]
    [DisableAuditing]
    public class RegisterDefineAdminModel : RegisterModelBase
    {
        [BindProperty]
        public PostInput Input { get; set; }

        public RegisterDefineAdminModel(): base()
        {
            Input = new PostInput();
        }

        public virtual async Task<IActionResult> OnGetAsync()
        {
            await LoadSettingsAsync();

            Input.AdminEmailAddress = TempData.Peek(nameof(PostInput.AdminEmailAddress))?.ToString() ?? string.Empty;
            Input.Password = TempData.Peek(nameof(PostInput.Password))?.ToString() ?? string.Empty;
            Input.ConfirmPassword = Input.Password;

            // Render page 
            return Page();
        }

        public virtual async Task<IActionResult> OnPostAsync(string action)
        {
            await LoadSettingsAsync();

            if (action == "Input")
            {
                try
                {
                    ValidateModel();

                    TempData[nameof(PostInput.AdminEmailAddress)] = Input.AdminEmailAddress;
                    TempData[nameof(PostInput.Password)] = Input.Password;

                    return RedirectToPage("./RegisterConfirm");
                }
                catch (AbpValidationException ex)
                {
                    // Handle this error.
                    foreach (var a in ex.ValidationErrors)
                    {
                        Alerts.Add(Volo.Abp.AspNetCore.Mvc.UI.Alerts.AlertType.Danger, a.ErrorMessage);
                    }
                }
            }
            else if (action == "Cancel")
            {
                return RedirectToPage("./Register");
            }

            return Page();
        }

        protected override void ValidateModel()
        {
            var passwordValidator = new PasswordValidator(PwdComplexity, L, ModelState, "Input.Password", "Input.ConfirmPassword");
            if (passwordValidator.ValidateComplexity(Input!.Password))
            {
                passwordValidator.ValidateConfirmPassword(Input.Password, Input!.ConfirmPassword);
            }

            base.ValidateModel();
        }

        protected override async Task LoadSettingsAsync()
        {
            await base.LoadSettingsAsync();
            await ReadInPasswordComplexityAsync();
        }

        public class PostInput : IHasConfirmPassword
        {
            [Required]
            [EmailAddress]
            [DynamicStringLength(typeof(IdentityUserConsts), nameof(IdentityUserConsts.MaxEmailLength))]
            public string? AdminEmailAddress { get; set; }

            [Required]
            [DynamicStringLength(typeof(IdentityUserConsts), nameof(IdentityUserConsts.MaxPasswordLength))]
            [DataType(DataType.Password)]
            [DisableAuditing]
            public string? Password { get; set; }

            [Required]
            [DynamicStringLength(typeof(IdentityUserConsts), nameof(IdentityUserConsts.MaxPasswordLength))]
            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "Password does NOT match!")]
            [DisableAuditing]
            public string? ConfirmPassword { get; set; }

        }
    }
}
