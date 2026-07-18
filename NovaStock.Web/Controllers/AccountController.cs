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

    // ─── PROFİL BİLGİSİ GETİR (AJAX) ────────────────────────────────────────────
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetProfileInfo()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        var role  = roles.FirstOrDefault() ?? "Dealer";

        string roleLabel = role switch
        {
            "Admin"           => "Administrator",
            "Dealer"          => "Ana Bayi",
            "DealerPurchase"  => "Satın Alma Personeli",
            "DealerFinance"   => "Muhasebe Personeli",
            _                 => role
        };

        string tierLabel = user.Tier switch
        {
            DealerTier.Gold   => "Gold",
            DealerTier.Silver => "Silver",
            _                 => "Bronze"
        };

        return Json(new
        {
            fullName     = user.FullName,
            email        = user.Email,
            phone        = user.PhoneNumber ?? user.Phone,
            address      = user.Address,
            role,
            roleLabel,
            tier         = tierLabel,
            isAdmin      = User.IsInRole("Admin"),
            isMainDealer = User.IsInRole("Dealer"),
            themeMode    = user.ThemeMode ?? "light",
            accentColor  = user.AccentColor ?? "#6366f1",
            companyName  = user.CompanyName
        });
    }

    // ─── PROFİL GÜNCELLE (AJAX POST) ─────────────────────────────────────────────
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileViewModel vm)
    {
        if (!ModelState.IsValid)
            return Json(new { success = false, message = "Form verisi geçersiz." });

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Json(new { success = false, message = "Kullanıcı bulunamadı." });

        user.FullName    = vm.FullName;
        user.PhoneNumber = vm.PhoneNumber;
        user.Address     = vm.Address;

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            _logger.LogInformation("Profil güncellendi: {User}", user.Email);
            return Json(new { success = true, message = "Profiliniz başarıyla güncellendi." });
        }

        return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
    }

    // ─── ŞİFRE DEĞİŞTİR (AJAX POST) ─────────────────────────────────────────────
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordViewModel vm)
    {
        if (!ModelState.IsValid)
            return Json(new { success = false, message = "Form verisi geçersiz." });

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Json(new { success = false, message = "Kullanıcı bulunamadı." });

        var result = await _userManager.ChangePasswordAsync(user, vm.CurrentPassword, vm.NewPassword);
        if (result.Succeeded)
        {
            _logger.LogInformation("Şifre değiştirildi: {User}", user.Email);
            return Json(new { success = true, message = "Şifreniz başarıyla değiştirildi." });
        }

        var errorMsg = result.Errors.FirstOrDefault()?.Description ?? "Şifre değiştirilemedi.";
        errorMsg = errorMsg.Contains("Incorrect password") ? "Mevcut şifreniz hatalı." : errorMsg;
        return Json(new { success = false, message = errorMsg });
    }

    // ─── TEMA TERCİHİ KAYDET (AJAX POST) ─────────────────────────────────────────
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> SaveThemePreference([FromBody] SaveThemeViewModel vm)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Json(new { success = false });

        user.ThemeMode   = vm.ThemeMode;
        user.AccentColor = vm.AccentColor;
        await _userManager.UpdateAsync(user);

        return Json(new { success = true });
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
