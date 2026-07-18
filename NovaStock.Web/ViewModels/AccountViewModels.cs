using System.ComponentModel.DataAnnotations;

namespace NovaStock.Web.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    public string Email    { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunludur.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

public class RegisterViewModel
{
    [Required]
    public string FullName    { get; set; } = string.Empty;

    [Required]
    public string CompanyName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email       { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(6)]
    public string Password    { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Şifreler eşleşmiyor.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? TaxNumber  { get; set; }
    public string? Phone      { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Mevcut şifre zorunludur.")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni şifre zorunludur.")]
    [DataType(DataType.Password)]
    [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalıdır.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre tekrarı zorunludur.")]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "Şifreler eşleşmiyor.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class UpdateProfileViewModel
{
    [Required(ErrorMessage = "Ad Soyad zorunludur.")]
    public string FullName    { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
    public string? Address     { get; set; }
}

public class SaveThemeViewModel
{
    public string ThemeMode  { get; set; } = "light";
    public string AccentColor { get; set; } = "#6366f1";
}

