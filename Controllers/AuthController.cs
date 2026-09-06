using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FutureTech.StudentManagement.Controllers;

public class AuthController : Controller
{
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _configuration;

    public AuthController(ILogger<AuthController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = string.IsNullOrWhiteSpace(returnUrl)
            ? Url.Action("Index", "Students")
            : returnUrl;

        ViewData["GoogleEnabled"] = !string.IsNullOrWhiteSpace(_configuration["Authentication:Google:ClientId"])
            && !string.IsNullOrWhiteSpace(_configuration["Authentication:Google:ClientSecret"]);
        ViewData["GitHubEnabled"] = !string.IsNullOrWhiteSpace(_configuration["Authentication:GitHub:ClientId"])
            && !string.IsNullOrWhiteSpace(_configuration["Authentication:GitHub:ClientSecret"]);

        return View();
    }

    [AllowAnonymous]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var enabledProviders = GetEnabledProviders();
        if (!enabledProviders.Contains(provider, StringComparer.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = $"The {provider} sign-in option is not configured.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth", new { returnUrl = returnUrl ?? "/Students" });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        properties.Items["LoginProvider"] = provider;
        return Challenge(properties, provider);
    }

    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (!string.IsNullOrWhiteSpace(remoteError))
        {
            _logger.LogWarning("External authentication error: {RemoteError}", remoteError);
            TempData["ErrorMessage"] = $"Sign-in failed: {remoteError}";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            TempData["ErrorMessage"] = "Authentication failed. Please try again.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
        var name = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name ?? "Administrator";
        var provider = User.FindFirstValue("urn:provider")
            ?? HttpContext.Features.Get<IAuthenticateResultFeature>()?.AuthenticateResult?.Ticket?.AuthenticationScheme
            ?? "OAuth";

        if (string.IsNullOrWhiteSpace(email))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["ErrorMessage"] = "Your sign-in provider did not return an email address.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        if (!IsAuthorizedAdmin(email))
        {
            _logger.LogWarning("Unauthorized admin login attempt: {Email}", email);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(AccessDenied));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, "Admin"),
            new("Provider", provider)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true, AllowRefresh = true });

        _logger.LogInformation("Admin {Email} logged in successfully using {Provider}", email, provider);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Students");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private bool IsAuthorizedAdmin(string email)
    {
        var allowedEmails = _configuration.GetSection("Auth:AllowedAdminEmails").Get<string[]>() ?? Array.Empty<string>();
        if (allowedEmails.Any(x => string.Equals(x, email, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var allowedDomains = _configuration.GetSection("Auth:AllowedEmailDomains").Get<string[]>() ?? Array.Empty<string>();
        return allowedDomains.Any(domain => email.EndsWith($"@{domain}", StringComparison.OrdinalIgnoreCase));
    }

    private HashSet<string> GetEnabledProviders()
    {
        var providers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(_configuration["Authentication:Google:ClientId"]) &&
            !string.IsNullOrWhiteSpace(_configuration["Authentication:Google:ClientSecret"]))
        {
            providers.Add("Google");
        }

        if (!string.IsNullOrWhiteSpace(_configuration["Authentication:GitHub:ClientId"]) &&
            !string.IsNullOrWhiteSpace(_configuration["Authentication:GitHub:ClientSecret"]))
        {
            providers.Add("GitHub");
        }

        return providers;
    }
}
