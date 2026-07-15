namespace NovaStock.Web.ViewModels;

// ─── Toplu Sipariş ────────────────────────────────────────────────────────────
public class BulkOrderRow
{
    public string SKU      { get; set; } = string.Empty;
    public int    Quantity { get; set; }
    public int    RowNumber { get; set; }
}

public class BulkOrderResultItem
{
    public string  SKU         { get; set; } = string.Empty;
    public string  ProductName { get; set; } = string.Empty;
    public int     Requested   { get; set; }
    public int     Available   { get; set; }
    public decimal UnitPrice   { get; set; }
    public bool    IsOk        => Available >= Requested;
    public string  StatusMsg   => IsOk
        ? $"✅ {Requested} adet – stok yeterli"
        : $"⚠ Talep: {Requested} – Stok: {Available}";
}

public class BulkOrderResultViewModel
{
    public List<BulkOrderResultItem> Items         { get; set; } = [];
    public List<string>              ExcelErrors   { get; set; } = [];
    public bool HasErrors      => ExcelErrors.Any() || Items.Any(i => !i.IsOk);
    public bool CanProceed     => Items.Any(i => i.IsOk);
    public int  ValidCount     => Items.Count(i => i.IsOk);
    public int  InvalidCount   => Items.Count(i => !i.IsOk);
}
