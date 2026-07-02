using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NovaStock.Web.Data;
using NovaStock.Web.Hubs;
using NovaStock.Web.Models;
using NovaStock.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── MVC ─────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ─── EF Core + SQLite ────────────────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ─── ASP.NET Core Identity ───────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit           = true;
    options.Password.RequiredLength         = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase       = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail         = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ─── Cookie Ayarları ─────────────────────────────────────────────────────────
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath         = "/Account/Login";
    options.LogoutPath        = "/Account/Logout";
    options.AccessDeniedPath  = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan    = TimeSpan.FromHours(8);
});

// ─── SignalR ──────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ─── HttpContextAccessor (Audit Log için) ────────────────────────────────────
builder.Services.AddHttpContextAccessor();

// ─── HttpClient (Döviz servisi için) ─────────────────────────────────────────
builder.Services.AddHttpClient();

// ─── Uygulama Servisleri ─────────────────────────────────────────────────────
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ExcelService>();
builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<PromotionEngine>();

// ─── Background Services ─────────────────────────────────────────────────────
builder.Services.AddHostedService<ExchangeRateBackgroundService>();

// ─── Session (isteğe bağlı sepet için) ───────────────────────────────────────
builder.Services.AddSession(options =>
{
    options.IdleTimeout        = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly    = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// ─── Middleware ───────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// ─── SignalR Hub Route ────────────────────────────────────────────────────────
app.MapHub<NotificationHub>("/notificationHub");

// ─── MVC Route ────────────────────────────────────────────────────────────────
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ─── Veritabanı Migrate + Seed ────────────────────────────────────────────────
await SeedDatabaseAsync(app);

app.Run();

// ─── İlk Çalışmada DB Oluştur + Admin Seed ────────────────────────────────────
static async Task SeedDatabaseAsync(WebApplication app)
{
    using var scope       = app.Services.CreateScope();
    var       context     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var       userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var       roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    // Migration uygula
    await context.Database.MigrateAsync();

    // Rolleri oluştur
    foreach (var role in new[] { "Admin", "Dealer" })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    // Admin kullanıcı oluştur (yoksa)
    const string adminEmail    = "admin@novastock.com";
    const string adminPassword = "Admin1234!";

    if (await userManager.FindByEmailAsync(adminEmail) is null)
    {
        var admin = new ApplicationUser
        {
            UserName    = adminEmail,
            Email       = adminEmail,
            FullName    = "NovaStock Admin",
            CompanyName = "NovaStock HQ",
            Tier        = DealerTier.Gold,
            IsActive    = true,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, adminPassword);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }

    // Demo Bayi (yoksa)
    const string dealerEmail    = "bayi@novastock.com";
    const string dealerPassword = "Bayi1234!";

    if (await userManager.FindByEmailAsync(dealerEmail) is null)
    {
        var dealer = new ApplicationUser
        {
            UserName    = dealerEmail,
            Email       = dealerEmail,
            FullName    = "Demo Bayi",
            CompanyName = "Demo Teknoloji A.Ş.",
            Tier        = DealerTier.Silver,
            IsActive    = true,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(dealer, dealerPassword);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(dealer, "Dealer");
    }
}
