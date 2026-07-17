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

    // Rolleri oluştur (Admin, Dealer, alt kullanıcı rolleri)
    foreach (var role in new[] { "Admin", "Dealer", "DealerPurchase", "DealerFinance" })
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

    // Demo Bayiler (yoksa)
    var demoDealers = new List<(string Email, string Name, string Company, DealerTier Tier)>
    {
        ("bayi@novastock.com", "Demo Bayi", "Demo Teknoloji A.Ş.", DealerTier.Silver),
        ("vatan@novastock.com", "Vatan Bilgisayar", "Vatan Bilgisayar San. ve Tic. A.Ş.", DealerTier.Gold),
        ("hepsiburada@novastock.com", "Hepsiburada", "D-Market Elektronik Hizm. ve Tic. A.Ş.", DealerTier.Gold),
        ("mediamarkt@novastock.com", "MediaMarkt", "MediaMarkt Turkey", DealerTier.Silver),
        ("teknosa@novastock.com", "Teknosa", "Teknosa İç ve Dış Tic. A.Ş.", DealerTier.Silver),
        ("itopya@novastock.com", "İtopya", "İtopya Bilgisayar", DealerTier.Bronze)
    };

    foreach (var d in demoDealers)
    {
        if (await userManager.FindByEmailAsync(d.Email) is null)
        {
            var dealer = new ApplicationUser
            {
                UserName = d.Email,
                Email = d.Email,
                FullName = d.Name,
                CompanyName = d.Company,
                Tier = d.Tier,
                IsActive = true,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(dealer, "Bayi1234!");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(dealer, "Dealer");
        }
    }

    // Demo Tedarikçiler (yoksa)
    if (!await context.Suppliers.AnyAsync())
    {
        var demoSuppliers = new List<Supplier>
        {
            new Supplier
            {
                Name = "Apple Inc. Türkiye",
                ContactPerson = "Kadir Eren",
                Phone = "+90 216 282 15 00",
                Email = "supply-turkey@apple.com",
                Address = "Büyükdere Cad. No:199 Levent 199 K:22-23 34394 Şişli/İstanbul",
                TaxNumber = "0680456123",
                Balance = 0,
                IsActive = true,
                Notes = "Genel Apple cihaz ve aksesuar tedarikçisi."
            },
            new Supplier
            {
                Name = "Huawei Telekomünikasyon Dış Tic. Ltd. Şti.",
                ContactPerson = "Burak Yılmaz",
                Phone = "+90 216 688 26 00",
                Email = "supply-tr@huawei.com",
                Address = "Saray Mah. Ahmet Tevfik İleri Cad. Onur Ofis Park Plaza No:10 Ümraniye/İstanbul",
                TaxNumber = "4650578912",
                Balance = 0,
                IsActive = true,
                Notes = "Mobil cihazlar, giyilebilir teknoloji ve network ekipmanları."
            },
            new Supplier
            {
                Name = "Realme Mobil İletişim Teknolojileri A.Ş.",
                ContactPerson = "Selin Şahin",
                Phone = "+90 212 900 12 34",
                Email = "tr-sales@realme.com",
                Address = "Esentepe Mah. Büyükdere Cad. Maya Akar Center No:100 Şişli/İstanbul",
                TaxNumber = "7350123456",
                Balance = 0,
                IsActive = true,
                Notes = "Akıllı telefonlar ve IoT ürünleri tedarikçisi."
            },
            new Supplier
            {
                Name = "MSI Bilişim Sistemleri San. ve Tic. A.Ş.",
                ContactPerson = "Emre Can",
                Phone = "+90 212 288 88 88",
                Email = "supply@msi-turkey.com.tr",
                Address = "Mecidiyeköy Mah. Büyükdere Cad. Cemal Sahir Sok. No:29 Şişli/İstanbul",
                TaxNumber = "6240987654",
                Balance = 0,
                IsActive = true,
                Notes = "Anakart, Ekran Kartı, Monitör ve Laptop sevkiyatı yapan ana kanal."
            }
        };

        await context.Suppliers.AddRangeAsync(demoSuppliers);
        await context.SaveChangesAsync();
    }

    // Demo Ürünleri (yoksa)
    var apple = await context.Suppliers.FirstOrDefaultAsync(s => s.Name.Contains("Apple"));
    var huawei = await context.Suppliers.FirstOrDefaultAsync(s => s.Name.Contains("Huawei"));
    var realme = await context.Suppliers.FirstOrDefaultAsync(s => s.Name.Contains("Realme"));
    var msi = await context.Suppliers.FirstOrDefaultAsync(s => s.Name.Contains("MSI"));

    var demoProducts = new List<Product>
    {
        new Product
        {
            Name = "iPhone 15 Pro Max 256GB",
            SKU = "APP-IPH15PM-256",
            Barcode = "190199000012",
            Description = "Apple A17 Pro çip, 6.7 inç Super Retina XDR ekran, Titanyum kasa, 256GB depolama.",
            BasePrice = 75000.00m,
            StockCount = 50,
            CriticalStockLevel = 5,
            CategoryId = 2, // Telefon
            SupplierId = apple?.Id,
            ImageUrl = "https://images.unsplash.com/photo-1695048133142-1a20484d2569?auto=format&fit=crop&q=80&w=300",
            IsActive = true
        },
        new Product
        {
            Name = "MacBook Pro 14 M3",
            SKU = "APP-MBP14M3-512",
            Barcode = "190199000029",
            Description = "Apple M3 çip, 8 CPU, 10 GPU, 8 GB Birleşik Bellek, 512 GB SSD, Uzay Grisi.",
            BasePrice = 62000.00m,
            StockCount = 30,
            CriticalStockLevel = 3,
            CategoryId = 3, // Bilgisayar
            SupplierId = apple?.Id,
            ImageUrl = "https://images.unsplash.com/photo-1517336714731-489689fd1ca8?auto=format&fit=crop&q=80&w=300",
            IsActive = true
        },
        new Product
        {
            Name = "Huawei MateBook D16 i7",
            SKU = "HUA-MBD16I7-16",
            Barcode = "6901443000045",
            Description = "Intel Core i7-13700H işlemci, 16 GB LPDDR4X RAM, 1 TB NVMe SSD, Intel Iris Xe Graphics, 16 inç Ekran.",
            BasePrice = 32000.00m,
            StockCount = 40,
            CriticalStockLevel = 4,
            CategoryId = 3, // Bilgisayar
            SupplierId = huawei?.Id,
            ImageUrl = "https://images.unsplash.com/photo-1588872657578-7efd1f1555ed?auto=format&fit=crop&q=80&w=300",
            IsActive = true
        },
        new Product
        {
            Name = "Huawei Watch GT 4",
            SKU = "HUA-WGT4-BLK",
            Barcode = "6901443000120",
            Description = "46mm Akıllı Saat, Siyah Paslanmaz Çelik Kasa, Siyah Floroelastomer Kordon, 14 Gün Pil Ömrü.",
            BasePrice = 7500.00m,
            StockCount = 100,
            CriticalStockLevel = 10,
            CategoryId = 5, // Aksesuar
            SupplierId = huawei?.Id,
            ImageUrl = "https://images.unsplash.com/photo-1579586337278-3befd40fd17a?auto=format&fit=crop&q=80&w=300",
            IsActive = true
        },
        new Product
        {
            Name = "Realme GT3 240W",
            SKU = "REA-GT3-240W",
            Barcode = "6973456000234",
            Description = "Snapdragon 8+ Gen 1, 16 GB RAM, 1 TB Depolama, 240W Dünyanın En Hızlı Şarj Desteği.",
            BasePrice = 28000.00m,
            StockCount = 25,
            CriticalStockLevel = 2,
            CategoryId = 2, // Telefon
            SupplierId = realme?.Id,
            ImageUrl = "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&q=80&w=300",
            IsActive = true
        },
        new Product
        {
            Name = "MSI Katana 15 B13V",
            SKU = "MSI-KAT15-4060",
            Barcode = "4719072002345",
            Description = "Intel Core i7-13620H, RTX 4060 8GB GDDR6, 16 GB DDR5 RAM, 1 TB PCIe SSD, 144Hz FHD Ekran.",
            BasePrice = 42000.00m,
            StockCount = 20,
            CriticalStockLevel = 2,
            CategoryId = 3, // Bilgisayar
            SupplierId = msi?.Id,
            ImageUrl = "https://images.unsplash.com/photo-1603302576837-37561b2e2302?auto=format&fit=crop&q=80&w=300",
            IsActive = true
        },
        new Product
        {
            Name = "MSI G274QPF E2 Gaming Monitör",
            SKU = "MSI-G274QPF",
            Barcode = "4719072005674",
            Description = "27 inç, 2K WQHD (2560x1440), Rapid IPS panel, 180Hz yenileme hızı, 1ms tepki süresi.",
            BasePrice = 9500.00m,
            StockCount = 35,
            CriticalStockLevel = 5,
            CategoryId = 1, // Elektronik
            SupplierId = msi?.Id,
            ImageUrl = "https://images.unsplash.com/photo-1527443224154-c4a3942d3acf?auto=format&fit=crop&q=80&w=300",
            IsActive = true
        }
    };

    foreach (var p in demoProducts)
    {
        if (!await context.Products.AnyAsync(x => x.SKU == p.SKU))
        {
            await context.Products.AddAsync(p);
            await context.SaveChangesAsync();

            // İstanbul Deposu'na (Id=1) stok ekle
            await context.ProductWarehouses.AddAsync(new ProductWarehouse
            {
                ProductId = p.Id,
                WarehouseId = 1,
                Quantity = p.StockCount,
                Location = "A-Blok"
            });
        }
    }
    await context.SaveChangesAsync();
}

