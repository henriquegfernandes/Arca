using System.Security.Claims;
using Arca.Application.Auth;
using Arca.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Arca.Web.Controllers;

public sealed class AuthController(AuthenticateUserUseCase authenticateUser) : Controller
{
    [AllowAnonymous]
    [HttpGet("/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost("/login")]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await authenticateUser.ExecuteAsync(
            new AuthenticateUserCommand(
                model.Email,
                model.Password,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString()),
            cancellationToken);

        if (result.IsFailure || result.Value is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Invalid email or password.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.Value.Id.ToString()),
            new(ClaimTypes.Name, result.Value.FullName),
            new(ClaimTypes.Email, result.Value.Email),
            new("arca:is_super_admin", result.Value.IsSuperAdmin.ToString())
        };

        claims.AddRange(result.Value.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return RedirectToLocal(model.ReturnUrl);
    }

    [Authorize]
    [HttpPost("/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet("/access-denied")]
    public IActionResult AccessDenied() => Content("Access denied.", "text/plain");

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return Redirect("/");
    }
}
