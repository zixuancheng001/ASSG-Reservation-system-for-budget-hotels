using Demo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Demo.Controllers;

[Authorize(Roles = "Member")]
public class CartController : Controller
{
    private readonly DB db;

    public CartController(DB db)
    {
        this.db = db;
    }

    // POST: Cart/Add
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Add(
        string productId,
        DateTime checkInDate,
        DateTime checkOutDate)
    {
        // Get current member email
        string? email = User.Identity?.Name;

        if (string.IsNullOrEmpty(email))
        {
            return RedirectToAction("Login", "Account");
        }

        // Check product
        var product = db.Products.Find(productId);

        if (product == null)
        {
            TempData["Error"] = "Room not found.";
            return RedirectToAction("Index", "Catalog");
        }

        // Check room availability and maintenance status
        if (!product.IsAvailable || product.IsMaintenance)
        {
            TempData["Error"] = "This room is currently unavailable or under maintenance.";
            return RedirectToAction("Details", "Catalog",
                new { id = productId });
        }

        // Check dates
        if (checkInDate.Date < DateTime.Today)
        {
            TempData["Error"] = "Check-in date cannot be in the past.";
            return RedirectToAction("Details", "Catalog",
                new { id = productId });
        }

        if (checkOutDate.Date <= checkInDate.Date)
        {
            TempData["Error"] =
                "Check-out date must be after check-in date.";

            return RedirectToAction("Details", "Catalog",
                new { id = productId });
        }

        // Check date conflict with other cart items
        var cartConflict = db.ShoppingCartItems
            .Any(i =>
                i.ProductId == productId &&
                i.CheckInDate < checkOutDate.Date &&
                i.CheckOutDate > checkInDate.Date);

        // Check date conflict with confirmed orders in OrderItems
        var orderConflict = db.OrderItems
            .Include(oi => oi.Order)
            .Any(oi =>
                oi.ProductId == productId &&
                oi.Order.Status != "Cancelled" &&
                oi.CheckInDate < checkOutDate.Date &&
                oi.CheckOutDate > checkInDate.Date);

        if (cartConflict || orderConflict)
        {
            TempData["Error"] =
                "This room is already reserved for the selected dates.";

            return RedirectToAction("Details", "Catalog",
                new { id = productId });
        }

        // Find existing cart
        var cart = db.ShoppingCarts
            .FirstOrDefault(c => c.MemberEmail == email);

        // Create cart if member does not have one
        if (cart == null)
        {
            cart = new ShoppingCart
            {
                MemberEmail = email
            };

            db.ShoppingCarts.Add(cart);
            db.SaveChanges();
        }

        // Add room to cart
        var item = new ShoppingCartItem
        {
            CartId = cart.CartId,
            ProductId = productId,
            Quantity = 1,
            CheckInDate = checkInDate.Date,
            CheckOutDate = checkOutDate.Date
        };

        db.ShoppingCartItems.Add(item);
        db.SaveChanges();

        TempData["Info"] = "Room added to cart successfully.";

        return RedirectToAction(nameof(Index));
    }

    // GET: Cart/Index
    public IActionResult Index()
    {
        string? email = User.Identity?.Name;

        if (string.IsNullOrEmpty(email))
        {
            return RedirectToAction("Login", "Account");
        }

        var cart = db.ShoppingCarts
            .FirstOrDefault(c => c.MemberEmail == email);

        if (cart == null)
        {
            return View(new List<ShoppingCartItem>());
        }

        var items = db.ShoppingCartItems
            .Include(i => i.Product)
            .Where(i => i.CartId == cart.CartId)
            .OrderBy(i => i.CheckInDate)
            .ToList();

        return View(items);
    }

    // POST: Cart/Remove
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Remove(int cartItemId)
    {
        string? email = User.Identity?.Name;

        if (string.IsNullOrEmpty(email))
        {
            return RedirectToAction("Login", "Account");
        }

        // Find the member's cart
        var cart = db.ShoppingCarts
            .FirstOrDefault(c => c.MemberEmail == email);

        if (cart == null)
        {
            return RedirectToAction(nameof(Index));
        }

        // Find the cart item and make sure it belongs to this member
        var item = db.ShoppingCartItems
            .FirstOrDefault(i =>
                i.CartItemId == cartItemId &&
                i.CartId == cart.CartId);

        if (item != null)
        {
            db.ShoppingCartItems.Remove(item);
            db.SaveChanges();

            TempData["Info"] = "Room removed from cart.";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Cart/Clear
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Clear()
    {
        string? email = User.Identity?.Name;

        if (string.IsNullOrEmpty(email))
        {
            return RedirectToAction("Login", "Account");
        }

        var cart = db.ShoppingCarts
            .FirstOrDefault(c => c.MemberEmail == email);

        if (cart != null)
        {
            var items = db.ShoppingCartItems
                .Where(i => i.CartId == cart.CartId)
                .ToList();

            if (items.Any())
            {
                db.ShoppingCartItems.RemoveRange(items);
                db.SaveChanges();
            }

            TempData["Info"] = "Shopping cart cleared.";
        }

        return RedirectToAction(nameof(Index));
    }
}