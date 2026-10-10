using Microsoft.AspNetCore.Mvc;
using PolpAbp.Framework.Mvc.Interceptors;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace PolpAbp.Presentation.Account.Web.Pages.Account
{
    public class LogoutModel : PolpAbpAccountPageModel
    {
        private readonly ILogoutInterceptor _interceptor;
        
        public LogoutModel(ILogoutInterceptor interceptor)
        {
            _interceptor = interceptor;
        }

        public virtual async Task<IActionResult> OnGetAsync()
        {
            // Will be able to handle regardless the user is authentictaed or not.
            await IdentitySecurityLogManager.SaveAsync(new IdentitySecurityLogContext()
            {
                Identity = IdentitySecurityLogIdentityConsts.Identity,
                Action = IdentitySecurityLogActionConsts.Logout
            });

            await _interceptor.BeforeSignOutAsync(HttpContext);

            await SignInManager.SignOutAsync();

            await _interceptor.AfterSignOutAsync(HttpContext);
            
            if (!string.IsNullOrEmpty(ReturnUrl))
            {
                return await RedirectSafelyAsync(ReturnUrl, ReturnUrlHash);
            }

            return RedirectToPage("./LogoutSuccess");
        }

        public virtual async Task<IActionResult> OnPostAsync()
        {
            // Will be able to handle regardless the user is authentictaed or not.
            await IdentitySecurityLogManager.SaveAsync(new IdentitySecurityLogContext()
            {
                Identity = IdentitySecurityLogIdentityConsts.Identity,
                Action = IdentitySecurityLogActionConsts.Logout
            });

            await _interceptor.BeforeSignOutAsync(HttpContext);

            await SignInManager.SignOutAsync();

            await _interceptor.AfterSignOutAsync(HttpContext);

            return new JsonResult(new
            {
                ok = true
            });
        }
    }
}
