using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NovaStock.Web.Models;
using System.Text.Json;

namespace NovaStock.Web.Data;

/// <summary>
/// Uygulama veritabanı context'i.
/// - Identity entegrasyonu
/// - Global Soft Delete query filter
/// - Otomatik Audit Log kaydı
/// - CreatedAt / UpdatedAt otomatik güncelleme
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // ─── DbSet'ler ──────────────────────────────────────────────────────────────
    public DbSet<Category>          Categories        { get; set; }
    public DbSet<Product>           Products          { get; set; }
    public DbSet<Order>             Orders            { get; set; }
    public DbSet<OrderItem>         OrderItems        { get; set; }
    public DbSet<Warehouse>         Warehouses        { get; set; }
    public DbSet<ProductWarehouse>  ProductWarehouses { get; set; }
    public DbSet<AuditLog>          AuditLogs         { get; set; }
    public DbSet<ExchangeRate>      ExchangeRates     { get; set; }
    public DbSet<Promotion>         Promotions        { get; set; }
    public DbSet<Supplier>          Suppliers         { get; set; }
    public DbSet<PurchaseOrder>     PurchaseOrders    { get; set; }
    public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }

    // ─── Model Yapılandırması ───────────────────────────────────────────────────
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Global Soft Delete Query Filter – IsDeleted=true olan kayıtlar otomatik gizlenir
        builder.Entity<Product>()         .HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Category>()        .HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Order>()           .HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<OrderItem>()       .HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Warehouse>()       .HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ProductWarehouse>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<ExchangeRate>()     .HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Promotion>()        .HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Supplier>()         .HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PurchaseOrder>()    .HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PurchaseOrderItem>().HasQueryFilter(e => !e.IsDeleted);

        // ProductWarehouse composite index
        builder.Entity<ProductWarehouse>()
            .HasIndex(pw => new { pw.ProductId, pw.WarehouseId })
            .IsUnique();

        // Order – ApplicationUser ilişkisi (cascade silmeyi kapat)
        builder.Entity<Order>()
            .HasOne(o => o.Dealer)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.DealerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Seed başlangıç kategorileri
        builder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Elektronik",       IconClass = "fa-microchip",      CreatedAt = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), UpdatedAt = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc) },
            new Category { Id = 2, Name = "Telefon",          IconClass = "fa-mobile-screen",  CreatedAt = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), UpdatedAt = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc) },
            new Category { Id = 3, Name = "Bilgisayar",       IconClass = "fa-laptop",         CreatedAt = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), UpdatedAt = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc) },
            new Category { Id = 4, Name = "Beyaz Eşya",       IconClass = "fa-blender",        CreatedAt = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), UpdatedAt = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc) },
            new Category { Id = 5, Name = "Aksesuar",         IconClass = "fa-headphones",     CreatedAt = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), UpdatedAt = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc) }
        );

        // Seed başlangıç depoları
        builder.Entity<Warehouse>().HasData(
            new Warehouse { Id = 1, Name = "İstanbul Ana Depo", City = "İstanbul", IsActive = true, CreatedAt = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), UpdatedAt = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc) },
            new Warehouse { Id = 2, Name = "Ankara Depo",       City = "Ankara",   IsActive = true, CreatedAt = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), UpdatedAt = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc) },
            new Warehouse { Id = 3, Name = "İzmir Depo",        City = "İzmir",    IsActive = true, CreatedAt = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), UpdatedAt = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc) }
        );
    }

    // ─── SaveChanges Override – Soft Delete + Audit Log + Timestamp ─────────────
    public override int SaveChanges()
    {
        HandleSoftDelete();
        UpdateTimestamps();
        WriteAuditLogs();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        HandleSoftDelete();
        UpdateTimestamps();
        WriteAuditLogs();
        return await base.SaveChangesAsync(cancellationToken);
    }

    // ─── Soft Delete: "Delete" durumundaki entity'leri fiziksel silmek yerine ───
    //     IsDeleted=true, DeletedAt=şimdiki zaman yap.
    private void HandleSoftDelete()
    {
        var deletedEntries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Deleted && e.Entity is BaseEntity)
            .ToList();

        foreach (var entry in deletedEntries)
        {
            var entity = (BaseEntity)entry.Entity;
            entry.State          = EntityState.Modified;
            entity.IsDeleted     = true;
            entity.DeletedAt     = DateTime.UtcNow;
            entity.DeletedBy     = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        }
    }

    // ─── Otomatik CreatedAt / UpdatedAt doldur ──────────────────────────────────
    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is BaseEntity && e.State is EntityState.Added or EntityState.Modified)
            .ToList();

        foreach (var entry in entries)
        {
            var entity = (BaseEntity)entry.Entity;
            entity.UpdatedAt = DateTime.UtcNow;

            if (entry.State == EntityState.Added)
                entity.CreatedAt = DateTime.UtcNow;
        }
    }

    // ─── Audit Log kaydı ────────────────────────────────────────────────────────
    private void WriteAuditLogs()
    {
        var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";

        var auditEntries = ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditLog &&
                        e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => new AuditLog
            {
                Timestamp   = DateTime.UtcNow,
                UserName    = userName,
                TableName   = entry.Entity.GetType().Name,
                Action      = entry.State.ToString(),
                RecordId    = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString(),
                OldValues   = entry.State == EntityState.Modified
                                ? JsonSerializer.Serialize(entry.Properties
                                    .Where(p => p.IsModified)
                                    .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue?.ToString()))
                                : null,
                NewValues   = entry.State != EntityState.Deleted
                                ? JsonSerializer.Serialize(entry.Properties
                                    .Where(p => p.IsModified || entry.State == EntityState.Added)
                                    .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue?.ToString()))
                                : null,
                Description = BuildDescription(entry, userName)
            })
            .ToList();

        AuditLogs.AddRange(auditEntries);
    }

    private static string BuildDescription(EntityEntry entry, string userName)
    {
        var entityName = entry.Entity.GetType().Name;
        return entry.State switch
        {
            EntityState.Added    => $"{userName} yeni bir {entityName} kaydı oluşturdu.",
            EntityState.Modified => $"{userName} bir {entityName} kaydını güncelledi.",
            EntityState.Deleted  => $"{userName} bir {entityName} kaydını sildi.",
            _                    => string.Empty
        };
    }
}
