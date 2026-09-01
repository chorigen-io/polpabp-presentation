using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

// On purpose, we do not include Filter in the namespace.
// So we do not need additional namespace in the pages.
namespace PolpAbp.Presentation.Account
{
    /// <summary>
    /// Redirects a request that carries no <c>UserName</c> or <c>EmailAddress</c> in its
    /// <b>query string</b>.
    /// </summary>
    /// <remarks>
    /// <b>Do not apply this to a page that keeps identifiers out of its URL.</b> As an
    /// <see cref="IAuthorizationFilter"/> it runs before model binding, so it can see neither the
    /// one-hop identifier hand-off nor a hidden form field — on such a page it bounces every
    /// request, GET and POST alike. Guard inside the PageModel instead, after the hand-off has
    /// been consumed; <c>LocalLoginModel</c> is the worked example.
    ///
    /// Retained as public API for consumers outside this repository.
    /// </remarks>
    public class UserNameOrEmailRequiredAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string _redirectUrl;

        // We may use DI. However, for the performance person, 
        // we let the caller to provide the input.
        public UserNameOrEmailRequiredAttribute(string redirectUrl = "/account/sign-in") {
            _redirectUrl = redirectUrl;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var userName = context.HttpContext.Request.Query["UserName"];
            if (string.IsNullOrEmpty(userName))
            {
                var email = context.HttpContext.Request.Query["EmailAddress"];
                if (string.IsNullOrEmpty(email))
                {
                    var originalQueryString = context.HttpContext.Request.QueryString;
                    context.Result = new RedirectResult(_redirectUrl + originalQueryString ?? "");
                }
            }
        }
    }
}
