using Demo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Demo.Controllers;

[Authorize(Roles = "Member")]
public class WishlistController(DB db) : Controller
{
    public IActionResult Index()
    {
        var email = User.Identity?.Name!;
        var items = db.WishlistItems.Include(x => x.Product)
            .Where(x => x.MemberEmail == email)
            .OrderByDescending(x => x.AddedAt).ToList();
        return View(items);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Toggle(string productId, string? returnUrl)
    {
        var email = User.Identity?.Name!;
        var item = db.WishlistItems.FirstOrDefault(x => x.MemberEmail == email && x.ProductId == productId);
        if (item == null)
        {
            if (db.Products.Any(p => p.ProductId == productId))
            {
                db.WishlistItems.Add(new WishlistItem { MemberEmail = email, ProductId = productId });
                TempData["Info"] = "Room added to your wishlist.";
            }
        }
        else
        {
            db.WishlistItems.Remove(item);
            TempData["Info"] = "Room removed from your wishlist.";
        }
        db.SaveChanges();
        return LocalRedirect(!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl : Url.Action(nameof(Index))!);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Remove(int id) => ToggleById(id);

    private IActionResult ToggleById(int id)
    {
        var email = User.Identity?.Name!;
        var item = db.WishlistItems.FirstOrDefault(x => x.WishlistItemId == id && x.MemberEmail == email);
        if (item != null)
        {
            db.WishlistItems.Remove(item);
            db.SaveChanges();
            TempData["Info"] = "Room removed from your wishlist.";
        }
        return RedirectToAction(nameof(Index));
    }
}
