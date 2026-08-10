using EfcomReport.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EfcomReport.Pages.Inventory;

public sealed class IndexModel(InventoryService inventory, CurrentUserService currentUser) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public IReadOnlyList<InventoryItemView> Items { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (await currentUser.GetAsync(User) is null) return Forbid();
        Items = await inventory.SearchAsync(Q);
        return Page();
    }

    public async Task<IActionResult> OnPostTakeAsync(int stockId, string? q)
    {
        var user = await currentUser.GetAsync(User);
        if (user is null) return Forbid();

        try
        {
            await inventory.TakeAsync(stockId, user.Email, HttpContext.RequestAborted);
            TempData["Message"] = "The item was marked as taken.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Message"] = exception.Message;
        }

        return RedirectToPage(new { q });
    }
}
