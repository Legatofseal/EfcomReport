using EfcomReport.Data;
using EfcomReport.Models;
using Microsoft.EntityFrameworkCore;

namespace EfcomReport.Services;

public static class InventoryMovementTypes
{
    public const string Receipt = "Receipt";
    public const string Take = "Take";
    public const string Transfer = "Transfer";
}

public sealed record InventoryStockView(int StockId, int LocationId, string LocationName, int Quantity);

public sealed record InventoryItemView(
    int Id,
    string PartNumber,
    string Description,
    string Tags,
    string Keywords,
    decimal? UnitCost,
    IReadOnlyList<InventoryStockView> Stocks)
{
    public int TotalQuantity => Stocks.Sum(x => x.Quantity);
}

public sealed record InventoryLocationView(int Id, string Name);

public sealed record InventoryMovementView(
    DateTime CreatedAtUtc,
    string PartNumber,
    string MovementType,
    int Quantity,
    string? FromLocation,
    string? ToLocation,
    string PerformedByEmail,
    string? Note);

public sealed record InventoryAdminSnapshot(
    IReadOnlyList<InventoryItemView> Items,
    IReadOnlyList<InventoryLocationView> Locations,
    IReadOnlyList<InventoryMovementView> Movements);

public sealed class InventoryService(AppDbContext db)
{
    public async Task<IReadOnlyList<InventoryItemView>> SearchAsync(string? search)
    {
        var query = db.InventoryItems
            .AsNoTracking()
            .Include(x => x.Stocks)
            .ThenInclude(x => x.Location)
            .Where(x => x.IsActive);

        var terms = (search ?? "")
            .Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (terms.Length > 0)
        {
            foreach (var term in terms)
            {
                query = query.Where(x =>
                    x.PartNumber.ToLower().Contains(term) ||
                    x.Description.ToLower().Contains(term) ||
                    x.Tags.ToLower().Contains(term) ||
                    x.Keywords.ToLower().Contains(term));
            }
        }

        var items = await query.OrderBy(x => x.PartNumber).ToListAsync();
        return items.Select(ToView).ToList();
    }

    public async Task<InventoryAdminSnapshot> GetAdminSnapshotAsync()
    {
        var items = await db.InventoryItems
            .AsNoTracking()
            .Include(x => x.Stocks)
            .ThenInclude(x => x.Location)
            .Where(x => x.IsActive)
            .OrderBy(x => x.PartNumber)
            .ToListAsync();
        var locations = await db.InventoryLocations
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new InventoryLocationView(x.Id, x.Name))
            .ToListAsync();
        var movementRows = await db.InventoryMovements
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .ToListAsync();
        var itemNames = items.ToDictionary(x => x.Id, x => x.PartNumber);
        var locationNames = locations.ToDictionary(x => x.Id, x => x.Name);
        var movements = movementRows.Select(x => new InventoryMovementView(
            x.CreatedAtUtc,
            itemNames.GetValueOrDefault(x.ItemId, ""),
            x.MovementType,
            x.Quantity,
            x.FromLocationId is { } fromId ? locationNames.GetValueOrDefault(fromId) : null,
            x.ToLocationId is { } toId ? locationNames.GetValueOrDefault(toId) : null,
            x.PerformedByEmail,
            x.Note)).ToList();

        return new InventoryAdminSnapshot(items.Select(ToView).ToList(), locations, movements);
    }

    public async Task AddOrIncreaseAsync(
        string partNumber,
        string description,
        string? tags,
        string? keywords,
        decimal? unitCost,
        string locationName,
        int quantity,
        string performedByEmail,
        CancellationToken cancellationToken = default)
    {
        partNumber = partNumber.Trim();
        description = description.Trim();
        locationName = locationName.Trim();
        tags = tags?.Trim() ?? "";
        keywords = keywords?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(partNumber)) throw new InvalidOperationException("Part number is required.");
        if (string.IsNullOrWhiteSpace(description)) throw new InvalidOperationException("Description is required.");
        if (string.IsNullOrWhiteSpace(locationName)) throw new InvalidOperationException("Location is required.");
        if (quantity <= 0) throw new InvalidOperationException("Quantity must be greater than zero.");
        if (unitCost < 0) throw new InvalidOperationException("Cost cannot be negative.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var item = await db.InventoryItems.SingleOrDefaultAsync(x => x.PartNumber == partNumber, cancellationToken);
        if (item is null)
        {
            item = new InventoryItem
            {
                PartNumber = partNumber,
                Description = description,
                Tags = tags,
                Keywords = keywords,
                UnitCost = unitCost,
                CreatedByEmail = performedByEmail,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.InventoryItems.Add(item);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            item.IsActive = true;
            item.Description = description;
            item.Tags = tags;
            item.Keywords = keywords;
            item.UnitCost = unitCost;
            item.UpdatedAtUtc = DateTime.UtcNow;
        }

        var location = await db.InventoryLocations.SingleOrDefaultAsync(x => x.Name == locationName, cancellationToken);
        if (location is null)
        {
            location = new InventoryLocation
            {
                Name = locationName,
                CreatedByEmail = performedByEmail,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.InventoryLocations.Add(location);
            await db.SaveChangesAsync(cancellationToken);
        }

        var stock = await db.InventoryStocks.SingleOrDefaultAsync(x =>
            x.ItemId == item.Id && x.LocationId == location.Id, cancellationToken);
        if (stock is null)
        {
            stock = new InventoryStock { ItemId = item.Id, LocationId = location.Id, Quantity = 0 };
            db.InventoryStocks.Add(stock);
        }
        stock.Quantity += quantity;
        stock.UpdatedAtUtc = DateTime.UtcNow;
        db.InventoryMovements.Add(new InventoryMovement
        {
            ItemId = item.Id,
            ToLocationId = location.Id,
            Quantity = quantity,
            MovementType = InventoryMovementTypes.Receipt,
            PerformedByEmail = performedByEmail,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task TakeAsync(int stockId, string performedByEmail, CancellationToken cancellationToken = default)
    {
        var stock = await db.InventoryStocks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == stockId, cancellationToken);
        if (stock is null) throw new InvalidOperationException("The selected stock record was not found.");
        await TakeAsync(stock.ItemId, stock.LocationId, 1, performedByEmail, cancellationToken);
    }

    public async Task TakeAsync(
        int itemId,
        int locationId,
        int quantity,
        string performedByEmail,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0) throw new InvalidOperationException("Quantity must be greater than zero.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var stock = await db.InventoryStocks
            .Include(x => x.Item)
            .Include(x => x.Location)
            .SingleOrDefaultAsync(x => x.ItemId == itemId && x.LocationId == locationId, cancellationToken);
        if (stock is null) throw new InvalidOperationException("The selected stock record was not found.");
        if (stock.Quantity < quantity) throw new InvalidOperationException("There is not enough quantity in this location.");

        stock.Quantity -= quantity;
        stock.UpdatedAtUtc = DateTime.UtcNow;
        db.InventoryMovements.Add(new InventoryMovement
        {
            ItemId = stock.ItemId,
            FromLocationId = stock.LocationId,
            Quantity = quantity,
            MovementType = InventoryMovementTypes.Take,
            PerformedByEmail = performedByEmail,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task MoveAsync(
        int itemId,
        int fromLocationId,
        string toLocationName,
        int quantity,
        string? note,
        string performedByEmail,
        CancellationToken cancellationToken = default)
    {
        toLocationName = toLocationName.Trim();
        if (string.IsNullOrWhiteSpace(toLocationName)) throw new InvalidOperationException("Destination location is required.");
        if (quantity <= 0) throw new InvalidOperationException("Quantity must be greater than zero.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var source = await db.InventoryStocks.SingleOrDefaultAsync(x =>
            x.ItemId == itemId && x.LocationId == fromLocationId, cancellationToken);
        if (source is null || source.Quantity < quantity)
            throw new InvalidOperationException("The source location does not contain enough quantity.");

        var destination = await db.InventoryLocations.SingleOrDefaultAsync(x => x.Name == toLocationName, cancellationToken);
        if (destination is null)
        {
            destination = new InventoryLocation
            {
                Name = toLocationName,
                CreatedByEmail = performedByEmail,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.InventoryLocations.Add(destination);
            await db.SaveChangesAsync(cancellationToken);
        }
        if (destination.Id == fromLocationId)
            throw new InvalidOperationException("The destination must be different from the source location.");

        var target = await db.InventoryStocks.SingleOrDefaultAsync(x =>
            x.ItemId == itemId && x.LocationId == destination.Id, cancellationToken);
        if (target is null)
        {
            target = new InventoryStock { ItemId = itemId, LocationId = destination.Id, Quantity = 0 };
            db.InventoryStocks.Add(target);
        }
        source.Quantity -= quantity;
        source.UpdatedAtUtc = DateTime.UtcNow;
        target.Quantity += quantity;
        target.UpdatedAtUtc = DateTime.UtcNow;
        db.InventoryMovements.Add(new InventoryMovement
        {
            ItemId = itemId,
            FromLocationId = fromLocationId,
            ToLocationId = destination.Id,
            Quantity = quantity,
            MovementType = InventoryMovementTypes.Transfer,
            PerformedByEmail = performedByEmail,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static InventoryItemView ToView(InventoryItem item) => new(
        item.Id,
        item.PartNumber,
        item.Description,
        item.Tags,
        item.Keywords,
        item.UnitCost,
        item.Stocks
            .Where(x => x.Quantity > 0 && x.Location.IsActive)
            .OrderBy(x => x.Location.Name)
            .Select(x => new InventoryStockView(x.Id, x.LocationId, x.Location.Name, x.Quantity))
            .ToList());
}
