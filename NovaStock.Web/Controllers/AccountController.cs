using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NovaStock.Web.Data;
using NovaStock.Web.Models;
using NovaStock.Web.ViewModels;

namespace NovaStock.Web.Controllers;

/// <summary>
/// Kimlik doğrulama – Giriş, Kayıt, Çıkış.
/// </summary>
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser>  _signIn;
    private readonly UserManager<ApplicationUser>    _userManager;
    private readonly RoleManager<IdentityRole>       _roleManager;
    private readonly ILogger<AccountController>      _logger;

    public AccountController(
        SignInManager<ApplicationUser>  signIn,
        UserManager<ApplicationUser>    userManager,
        RoleManager<IdentityRole>       roleManager,
        ILogger<AccountController>      logger)
    {
        _signIn      = signIn;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger      = logger;
    }

    // ─── GİRİŞ GET ───────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    // ─── GİRİŞ POST ──────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _signIn.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("Kullanıcı giriş yaptı: {Email}", model.Email);
            return LocalRedirect(returnUrl ?? "/");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError("", "Hesabınız kilitlendi. Lütfen daha sonra tekrar deneyin.");
        }
        else
        {
            ModelState.AddModelError("", "E-posta veya şifre hatalı.");
        }

        return View(model);
    }

    // ─── KAYIT GET ───────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Register() => View();

    // ─── KAYIT POST ──────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // Roller yoksa oluştur
        await EnsureRolesAsync();

        var user = new ApplicationUser
        {
            UserName    = model.Email,
            Email       = model.Email,
            FullName    = model.FullName,
            CompanyName = model.CompanyName,
            TaxNumber   = model.TaxNumber,
            Phone       = model.Phone,
            Tier        = DealerTier.Bronze
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "Dealer");
            await _signIn.SignInAsync(user, isPersistent: false);
            _logger.LogInformation("Yeni bayi kaydı: {Email}", model.Email);
            return RedirectToAction("Index", "Home");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);

        return View(model);
    }

    // ─── ÇIKIŞ ───────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // ─── Roller oluştur (ilk kurulum) ────────────────────────────────────────────
    private async Task EnsureRolesAsync()
    {
        foreach (var role in new[] { "Admin", "Dealer" })
        {
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}
