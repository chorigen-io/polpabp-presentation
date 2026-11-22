using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using PolpAbp.Framework.Mvc.Cookies;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Account.Settings;
using Volo.Abp.Auditing;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.TenantManagement;
using Volo.Abp.Validation;

namespace PolpAbp.Presentation.Account.Web.Pages.Account
{
    [UnauthenticatedUser]
    [DisableAuditing]
    public class RegisterModel : RegisterModelBase
    {
        [BindProperty]
        public PostInput Input { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool IsExternalLogin { get; set; }

        public bool IsRegistrationEnabled { get; set; }

        /// <summary>
        /// Set when the posted name is one another organization already uses: the page stays on
        /// this step and offers the next step (find that organization, or choose another name).
        /// </summary>
        public bool IsTenantNameTaken { get; set; }

        protected readonly IAppCookieManager CookieManager;

        protected ITenantNormalizer TenantNormalizer => LazyServiceProvider.LazyGetRequiredService<ITenantNormalizer>();

        public RegisterModel(IAppCookieManager cookieManager) : base()
        {
            Input = new PostInput();
            CookieManager = cookieManager;
        }


        public virtual async Task<IActionResult> OnGetAsync()
        {
            await LoadSettingsAsync();
            // Shortcut
            if (!IsRegistrationEnabled)
            {
                Alerts.Warning("Registration is not available now. Please try it later!");
            }

            // Clean up the cookie
            CookieManager.SetTenantCookieValue(Response, string.Empty);

            Input.TenantName = TempData.Peek(nameof(PostInput.TenantName))?.ToString() ?? string.Empty;
            // Render page 
            return Page();
        }

        public virtual async Task<IActionResult> OnPostAsync(string action)
        {
            await LoadSettingsAsync();

            // Shortcut
            if (!IsRegistrationEnabled)
            {
                Alerts.Warning("Registration is not available now. Please try it later!");
                return Page();
            }

            if (action == "Input")
            {
                try
                {
                    // Validate model
                    ValidateModel();

                    // Check availability here, where the step promises it, rather than leaving the
                    // first check to the confirm step's tenant creation.
                    if (await IsTenantNameTakenAsync(Input.TenantName!))
                    {
                        IsTenantNameTaken = true;
                        return Page();
                    }

                    if (!string.Equals(TempData.Peek(nameof(PostInput.TenantName))?.ToString(), Input.TenantName))
                    {
                        // Clean up data 
                        _ = TempData["Password"];
                    }

                    TempData[nameof(PostInput.TenantName)] = Input.TenantName;

                    // Success and then instructions
                    return RedirectToPage("./RegisterDefineAdmin");
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

            return Page();
        }

        protected virtual async Task<bool> IsTenantNameTakenAsync(string tenantName)
        {
            // The same normalized-name lookup TenantManager makes when it creates the tenant.
            var existing = await TenantRepository.FindByNameAsync(TenantNormalizer.NormalizeName(tenantName), includeDetails: false);
            return existing != null;
        }

        protected override async Task LoadSettingsAsync()
        {
            await base.LoadSettingsAsync();

            IsRegistrationEnabled = await SettingProvider.GetAsync<bool>(AccountSettingNames.IsSelfRegistrationEnabled);
        }

        public class PostInput
        {
            [Required]
            [MinLength(5)]
            [MaxLength(256)]
            public string? TenantName { get; set; }
        }
    }
}
