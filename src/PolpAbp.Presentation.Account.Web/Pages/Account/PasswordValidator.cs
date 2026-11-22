using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using PolpAbp.Framework.Security;

namespace PolpAbp.Presentation.Account.Web.Pages.Account
{
    public class PasswordValidator
    {
        private readonly PasswordComplexitySetting _setting;
        private readonly IStringLocalizer L;
        private readonly ModelStateDictionary ModelState;
        private readonly string _passwordKey;
        private readonly string _confirmPasswordKey;

        /// <param name="passwordKey">The ModelState key the page binds the password to (e.g. "Input.Password"),
        /// so the errors render in that field's validation span.</param>
        /// <param name="confirmPasswordKey">The ModelState key the page binds the confirmation to.</param>
        public PasswordValidator(PasswordComplexitySetting settings, IStringLocalizer l, ModelStateDictionary modelState,
            string passwordKey, string confirmPasswordKey)
        {
            _setting = settings;
            L = l;
            ModelState = modelState;
            _passwordKey = passwordKey;
            _confirmPasswordKey = confirmPasswordKey;
        }

        public bool ValidateComplexity(string? password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(_passwordKey, "Password is required");
                return false;
            }

            if (password.Length < _setting.RequiredLength)
            {
                ModelState.AddModelError(_passwordKey, L["The field {0} must be a string with a minimum length of {1}.", "Password", _setting.RequiredLength]);
                return false;
            }

            if (_setting.RequireUppercase && !password.Any(char.IsUpper))
            {
                ModelState.AddModelError(_passwordKey, "An upper case letter is required in the password!");
                return false;
            }

            if (_setting.RequireLowercase && !password.Any(char.IsLower))
            {
                ModelState.AddModelError(_passwordKey, "A lower case letter is required in the password!");
                return false;
            }

            if (_setting.RequireDigit && !password.Any(char.IsNumber))
            {
                ModelState.AddModelError(_passwordKey, "A number (letter) is required in the password!");
                return false;
            }

            if (_setting.RequireNonAlphanumeric && password.All(char.IsLetterOrDigit))
            {
                ModelState.AddModelError(_passwordKey, "A special symbol is required in the password!");
                return false;
            }

            return true;
        }

        public bool ValidateConfirmPassword(string? password, string? confirmPassword)
        {
            if (!Equals(password, confirmPassword))
            {
                ModelState.AddModelError(_confirmPasswordKey, L["'{0}' and '{1}' do not match.", "Confirm Password", "Password"]);
                return false;
            }

            return true;
        }
    }
}
