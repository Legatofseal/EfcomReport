namespace EfcomReport.Models;

public sealed class InventoryItem
{
    public int Id { get; set; }
    public string PartNumber { get; set; } = "";
    public string Description { get; set; } = "";
    public string Tags { get; set; } = "";
    public decimal? UnitCost { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByEmail { get; set; } = "";
    public ICollection<InventoryStock> Stocks { get; set; } = [];
}

public sealed class InventoryLocation
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByEmail { get; set; } = "";
    public ICollection<InventoryStock> Stocks { get; set; } = [];
}

public sealed class InventoryStock
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public InventoryItem Item { get; set; } = null!;
    public int LocationId { get; set; }
    public InventoryLocation Location { get; set; } = null!;
    public int Quantity { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class InventoryMovement
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public InventoryItem Item { get; set; } = null!;
    public int? FromLocationId { get; set; }
    public InventoryLocation? FromLocation { get; set; }
    public int? ToLocationId { get; set; }
    public InventoryLocation? ToLocation { get; set; }
    public int Quantity { get; set; }
    public string MovementType { get; set; } = "";
    public string PerformedByEmail { get; set; } = "";
    public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
