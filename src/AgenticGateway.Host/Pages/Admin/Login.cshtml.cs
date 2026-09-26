using System.Security.Claims;
using AgenticGateway.Host.Admin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgenticGateway.Host.Pages.Admin;

public sealed class LoginModel(AdminCredential credential) : PageModel
{
    [BindProperty] public string Key { get; set; } = "";
    public string? Error { get; private set; }
    public string KeyLocation => credential.IsFileBacked ? credential.KeyFilePath : "AGENTIC_GATEWAY_ADMIN_KEY";

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToPage("/Admin/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        if (!credential.Matches(Key))
        {
            Error = "Key quản trị không đúng. Kiểm tra file hoặc biến môi trường bên dưới.";
            return Page();
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "local-admin")],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/admin");
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Admin/Login");
    }
}
