using System.Security.Claims;
using System.Security.Cryptography;
using Arca.Application.Abstractions;
using Arca.Application.Auth;
using Arca.Application.Tenancy;
using Arca.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Arca.Web.Controllers;

public sealed class AuthController(
    AuthenticateUserUseCase authenticateUser,
    TenantSetupService tenantSetupService,
    PasswordSetupService passwordSetupService,
    IEmailSender emailSender,
    IConfiguration configuration) : Controller
{
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
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
    [EnableRateLimiting("auth")]
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

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpGet("/setup")]
    public IActionResult Setup()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect("/");
        }

        return View(new PublicTenantSetupViewModel());
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("/setup")]
    public async Task<IActionResult> Setup(PublicTenantSetupViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var command = new CreateTenantSetupCommand
        {
            Company = new CompanySetupStep
            {
                Name = model.CompanyName,
                LegalName = model.LegalName,
                Document = model.Document,
                Slug = model.Slug,
                Email = model.CompanyEmail,
                Phone = model.CompanyPhone,
                MainSegment = model.MainSegment
            },
            Settings = new TenantSettingsSetupStep
            {
                Currency = model.Currency,
                TimeZone = model.TimeZone,
                DefaultLanguage = model.DefaultLanguage,
                AllowMultipleStores = model.AllowMultipleStores,
                AllowBatchControl = model.AllowBatchControl,
                AllowExpirationControl = model.AllowExpirationControl,
                AllowStoreSpecificPricing = model.AllowStoreSpecificPricing
            },
            Stores =
            [
                new StoreSetupStep
                {
                    Name = model.StoreName,
                    Code = model.StoreCode,
                    Document = model.StoreDocument,
                    Email = model.StoreEmail,
                    Phone = model.StorePhone,
                    AddressLine = model.AddressLine,
                    City = model.City,
                    State = model.State,
                    ZipCode = model.ZipCode,
                    Type = model.StoreType
                }
            ],
            Administrator = new AdministratorSetupStep
            {
                FullName = model.AdminFullName,
                Email = model.AdminEmail,
                Phone = model.AdminPhone,
                TemporaryPassword = GenerateTemporaryPassword(),
                SendInviteEmail = false
            },
            Catalog = new InitialCatalogSetupStep { Template = model.CatalogTemplate },
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };

        var setupResult = await tenantSetupService.SetupAsync(command, cancellationToken);
        if (setupResult.IsFailure || setupResult.Value is null)
        {
            ModelState.AddModelError(string.Empty, setupResult.Error ?? "Tenant setup failed.");
            return View(model);
        }

        var tokenResult = await passwordSetupService.CreateTokenAsync(
            setupResult.Value.AdministratorUserId,
            TimeSpan.FromHours(configuration.GetValue("PasswordSetup:TokenHours", 48)),
            cancellationToken);

        if (tokenResult.IsFailure || tokenResult.Value is null)
        {
            ModelState.AddModelError(string.Empty, tokenResult.Error ?? "Password setup link could not be created.");
            return View(model);
        }

        var setupUrl = BuildPasswordSetupUrl(tokenResult.Value.Token);
        await emailSender.SendAsync(
            model.AdminEmail,
            $"Finalize seu acesso ao Arca - {model.CompanyName}",
            BuildPasswordSetupEmail(model.AdminFullName, model.CompanyName, setupUrl),
            cancellationToken);

        TempData["SetupMessage"] = "Cadastro recebido. Enviamos um link para definir a senha do administrador.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpGet("/set-password")]
    public IActionResult SetPassword([FromQuery] string token)
    {
        return View(new SetPasswordViewModel { Token = token });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("/set-password")]
    public async Task<IActionResult> SetPassword(SetPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await passwordSetupService.SetPasswordAsync(
            model.Token,
            model.NewPassword,
            model.ConfirmPassword,
            cancellationToken);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to set password.");
            return View(model);
        }

        TempData["SetupMessage"] = "Senha definida com sucesso. Entre com seu e-mail e senha.";
        return RedirectToAction(nameof(Login));
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return Redirect("/");
    }

    private string BuildPasswordSetupUrl(string token)
    {
        var baseUrl = configuration["App:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = $"{Request.Scheme}://{Request.Host}";
        }

        return $"{baseUrl.TrimEnd('/')}/set-password?token={Uri.EscapeDataString(token)}";
    }

    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$";
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return new string(bytes.ToArray().Select(value => chars[value % chars.Length]).ToArray());
    }

    private static string BuildPasswordSetupEmail(string fullName, string companyName, string setupUrl)
    {
        return $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"></head>
        <body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #f4f6f8; padding: 40px;">
            <div style="max-width: 560px; margin: auto; background: #fff; border: 1px solid #e3c96b; border-radius: 8px; padding: 32px; box-shadow: 0 4px 12px rgba(0,0,0,0.08);">
                <div style="font-size: 28px; font-weight: 750; margin-bottom: 8px; color: #16324F;">Arca</div>
                <p style="color: #5f6b7a; margin: 0 0 24px;">Sua plataforma de estoque está pronta.</p>
                <h2 style="margin: 0 0 16px; color: #16324F;">Olá, {fullName}!</h2>
                <p style="line-height: 1.6; color: #1f2933; margin: 0 0 16px;">
                    O cadastro de <strong>{companyName}</strong> foi criado no Arca. Defina sua senha para acessar o painel administrativo.
                </p>
                <p style="margin: 28px 0;">
                    <a href="{setupUrl}" style="background: #16324F; color: #fff; text-decoration: none; padding: 12px 18px; border-radius: 6px; font-weight: 750;">Definir senha</a>
                </p>
                <p style="color: #8f9aa8; font-size: 13px; margin: 0;">Este link expira em breve e só pode ser usado uma vez.</p>
            </div>
        </body>
        </html>
        """;
    }
}
