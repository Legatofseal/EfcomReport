using EfcomReport.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EfcomReport.Pages.Admin;

public sealed class InventoryModel(InventoryService inventory, CurrentUserService currentUser) : PageModel
{
    [BindProperty] public string PartNumber { get; set; } = "";
    [BindProperty] public string Description { get; set; } = "";
    [BindProperty] public string? Tags { get; set; }
    [BindProperty] public decimal? UnitCost { get; set; }
    [BindProperty] public string LocationName { get; set; } = "";
    [BindProperty] public int Quantity { get; set; } = 1;

    [BindProperty] public int MoveItemId { get; set; }
    [BindProperty] public int MoveFromLocationId { get; set; }
    [BindProperty] public string MoveToLocationName { get; set; } = "";
    [BindProperty] public int MoveQuantity { get; set; } = 1;
    [BindProperty] public string? MoveNote { get; set; }

    public IReadOnlyList<InventoryItemView> Items { get; private set; } = [];
    public IReadOnlyList<InventoryLocationView> Locations { get; private set; } = [];
    public IReadOnlyList<InventoryMovementView> Movements { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await IsAdminAsync()) return Forbid();
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        var user = await currentUser.GetAsync(User);
        if (user is null || user.Role != "Admin") return Forbid();

        try
        {
            await inventory.AddOrIncreaseAsync(PartNumber, Description, Tags, UnitCost, LocationName, Quantity, user.Email, HttpContext.RequestAborted);
            TempData["Message"] = "Inventory item saved and quantity added.";
            return RedirectToPage();
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostMoveAsync()
    {
        var user = await currentUser.GetAsync(User);
        if (user is null || user.Role != "Admin") return Forbid();

        try
        {
            await inventory.MoveAsync(MoveItemId, MoveFromLocationId, MoveToLocationName, MoveQuantity, MoveNote, user.Email, HttpContext.RequestAborted);
            TempData["Message"] = "Inventory item moved.";
            return RedirectToPage();
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await LoadAsync();
            return Page();
        }
    }

    private async Task<bool> IsAdminAsync()
    {
        var user = await currentUser.GetAsync(User);
        return user?.Role == "Admin";
    }

    private async Task LoadAsync()
    {
        var snapshot = await inventory.GetAdminSnapshotAsync();
        Items = snapshot.Items;
        Locations = snapshot.Locations;
        Movements = snapshot.Movements;
    }
}
