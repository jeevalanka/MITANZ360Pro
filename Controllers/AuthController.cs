using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using MITANZ360Pro.Web.Data;

namespace MITANZ360Pro.Web.Controllers;

[Route("auth")]
public class AuthController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;

    private readonly UserManager<ApplicationUser> _userManager;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;

        _userManager = userManager;
    }

    // =========================================
    // LOGIN
    // =========================================

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        string email,
        string password)
    {
        var user =
            await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            return Redirect("/xlogin?error=Invalid login");
        }

        var result =
            await _signInManager.PasswordSignInAsync(
                user.UserName!,
                password,
                true,
                false);

        if (!result.Succeeded)
        {
            return Redirect("/xlogin?error=Invalid login");
        }

        return Redirect("/");
    }

    // =========================================
    // LOGOUT
    // =========================================

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();

        return Redirect("/guest-dashboard");
    }
}