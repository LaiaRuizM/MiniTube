using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MiniTube.Pages.Account;

public class LoginModel : PageModel
{
    public IActionResult OnGet(string? returnUrl = "/")
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = returnUrl
        };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }
}
