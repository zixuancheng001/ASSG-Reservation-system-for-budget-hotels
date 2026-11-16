using Demo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace Demo.Controllers;

public class OrderController : Controller
{
    private readonly DB db;
    private readonly Helper hp;

    public OrderController(DB db, Helper hp)
    {
        this.db = db;
        this.hp = hp;
    }

    // ========================================================================
    // Administrator-only feature (accessible only to admins)
    // ========================================================================

    [Authorize(Roles = "Admin")]
    public IActionResult Index()
    {
        return View();
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public IActionResult GetOrdersJson(string? query, string? status, string? sortBy, int page = 1)
    {
        int pageSize = 8;
        page = Math.Max(page, 1);

        var ordersQuery = db.Orders
            .Include(o => o.Member)
            .Include(o => o.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            // Match on order ID (numeric) or member email
            if (int.TryParse(query.Trim(), out int orderId))
            {
                ordersQuery = ordersQuery.Where(o => o.OrderId == orderId);
            }
            else
            {
                ordersQuery = ordersQuery.Where(o => o.MemberEmail.Contains(query));
            }
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            ordersQuery = ordersQuery.Where(o => o.Status == status);
        }

        ordersQuery = sortBy switch
        {
            "date_asc"    => ordersQuery.OrderBy(o => o.OrderDate),
            "amount_asc"  => ordersQuery.OrderBy(o => o.TotalAmount),
            "amount_desc" => ordersQuery.OrderByDescending(o => o.TotalAmount),
            "id_desc"     => ordersQuery.OrderByDescending(o => o.OrderId),
            _             => ordersQuery.OrderBy(o => o.OrderId)  // id_asc default
        };

        int totalItems = ordersQuery.Count();
        int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        if (totalPages > 0 && page > totalPages) page = totalPages;

        var items = ordersQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                orderId     = o.OrderId,
                memberEmail = o.MemberEmail,
                orderDate   = o.OrderDate,
                status      = o.Status,
                totalAmount = o.TotalAmount,
                itemCount   = o.Items.Count
            })
            .ToList();

        return Json(new
        {
            items,
            currentPage = page,
            totalPages,
            totalItems
        });
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Edit(int id)
    {
        var o = db.Orders.Find(id);
        if (o == null) return RedirectToAction(nameof(Index));

        var vm = new OrderVM
        {
            OrderId = o.OrderId,
            MemberEmail = o.MemberEmail,
            OrderDate = o.OrderDate,
            Status = o.Status,
            TotalAmount = o.TotalAmount,
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public IActionResult Edit(OrderVM vm)
    {
        var o = db.Orders.Find(vm.OrderId);
        if (o == null) return RedirectToAction(nameof(Index));

        ModelState.Remove(nameof(vm.MemberEmail));

        if (ModelState.IsValid)
        {
            o.Status = vm.Status == "Paid" ? "Completed" : vm.Status;
            db.SaveChanges();

            TempData["Info"] = "Order updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public IActionResult Delete(int id)
    {
        var o = db.Orders
            .Include(o => o.Items)
            .FirstOrDefault(o => o.OrderId == id);

        if (o != null)
        {
            db.OrderItems.RemoveRange(o.Items);
            db.Orders.Remove(o);
            db.SaveChanges();

            TempData["Info"] = "Order deleted successfully.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ========================================================================
    // Member-exclusive feature (accessible to members or logged-in users)
    // ========================================================================

    [Authorize] // Simply log in
    [HttpGet]
    public IActionResult Checkout()
    {
        string userEmail = User.Identity?.Name ?? "";

        var cart = db.ShoppingCarts.FirstOrDefault(c => c.MemberEmail == userEmail);

        if (cart == null)
        {
            TempData["Info"] = "Your shopping cart is empty. Please select a room first.";
            return RedirectToAction("Index", "Home");
        }

        var cartItems = db.ShoppingCartItems
            .Include(i => i.Product)
            .Where(i => i.CartId == cart.CartId)
            .ToList();

        if (!cartItems.Any())
        {
            TempData["Info"] = "Your shopping cart is empty. Please select a room first.";
            return RedirectToAction("Index", "Home");
        }

        decimal grandTotal = cartItems.Sum(item =>
            (decimal)(item.CheckOutDate - item.CheckInDate).Days * item.Product.Price * item.Quantity
        );

        ViewBag.CartItems = cartItems;
        ViewBag.GrandTotal = grandTotal;
        ViewBag.DiscountAmount = 0m;

        var vm = new CheckoutViewModel
        {
            Email = userEmail
        };

        return View(vm);
    }

    // GET: Order/GetAvailableCoupons — returns active, non-expired coupons for the promo modal
    [Authorize]
    [HttpGet]
    public IActionResult GetAvailableCoupons()
    {
        var coupons = db.Coupons
            .Where(c => c.IsActive && (c.ExpiryDate == null || c.ExpiryDate >= DateTime.Today))
            .OrderBy(c => c.Code)
            .Select(c => new
            {
                code            = c.Code,
                discountPercent = c.DiscountPercent.ToString("0.##"),
                expiryText      = c.ExpiryDate == null
                                    ? "No expiry date"
                                    : "Valid until " + c.ExpiryDate.Value.ToString("dd MMM yyyy")
            })
            .ToList();

        return Json(coupons);
    }

    [Authorize]
    [HttpPost]
    public IActionResult ApplyCoupon(string? code)
    {
        var email = User.Identity?.Name ?? "";
        var cart = db.ShoppingCarts.FirstOrDefault(c => c.MemberEmail == email);
        var items = cart == null ? new List<ShoppingCartItem>() : db.ShoppingCartItems
            .Include(i => i.Product).Where(i => i.CartId == cart.CartId).ToList();
        var subtotal = items.Sum(item => (decimal)(item.CheckOutDate - item.CheckInDate).Days * item.Product.Price * item.Quantity);
        if (string.IsNullOrWhiteSpace(code))
            return Json(new { success = false, message = "Enter a promo code.", subtotal, discount = 0m, total = subtotal });

        var coupon = db.Coupons.FirstOrDefault(c => c.Code == code.Trim().ToUpperInvariant() && c.IsActive &&
            (c.ExpiryDate == null || c.ExpiryDate >= DateTime.Today));
        if (coupon == null)
            return Json(new { success = false, message = "This promo code is invalid or has expired.", subtotal, discount = 0m, total = subtotal });

        var discount = Math.Round(subtotal * coupon.DiscountPercent / 100m, 2);
        return Json(new { success = true, message = $"{coupon.DiscountPercent:0.##}% discount applied.", subtotal, discount, total = subtotal - discount, code = coupon.Code });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Checkout(CheckoutViewModel vm)
    {
        string userEmail = User.Identity?.Name ?? vm.Email;

        var cart = db.ShoppingCarts.FirstOrDefault(c => c.MemberEmail == userEmail);

        if (cart == null)
        {
            TempData["Info"] = "Your cart is empty.";
            return RedirectToAction("Index", "Home");
        }

        var cartItems = db.ShoppingCartItems
            .Include(i => i.Product)
            .Where(i => i.CartId == cart.CartId)
            .ToList();

        if (!cartItems.Any())
        {
            TempData["Info"] = "Your cart is empty.";
            return RedirectToAction("Index", "Home");
        }

        // 1. Remove unnecessary validation errors based on selected payment method
        if (vm.PaymentMethod == "FPX" || vm.PaymentMethod == "TnG")
        {
            ModelState.Remove(nameof(vm.CardName));
            ModelState.Remove(nameof(vm.CardNumber));
            ModelState.Remove(nameof(vm.ExpiryDate));
            ModelState.Remove(nameof(vm.CVV));
        }

        // 2. Validate room availability conflict
        foreach (var item in cartItems)
        {
            bool isConflict = db.OrderItems.Any(oi =>
                oi.ProductId == item.ProductId &&
                oi.Order.Status != "Cancelled" &&
                item.CheckInDate < oi.CheckOutDate &&
                item.CheckOutDate > oi.CheckInDate
            );

            if (isConflict)
            {
                ModelState.AddModelError("", $"The room '{item.Product?.Name}' is no longer available for the selected dates ({item.CheckInDate:dd MMM} - {item.CheckOutDate:dd MMM yyyy}). Please choose another date or room.");
            }
        }

        if (ModelState.IsValid)
        {
            decimal grandTotal = cartItems.Sum(item =>
                (decimal)(item.CheckOutDate - item.CheckInDate).Days * item.Product.Price * item.Quantity
            );

            Coupon? coupon = null;
            if (!string.IsNullOrWhiteSpace(vm.CouponCode))
            {
                var normalizedCode = vm.CouponCode.Trim().ToUpperInvariant();
                coupon = db.Coupons.FirstOrDefault(c => c.Code == normalizedCode && c.IsActive &&
                    (c.ExpiryDate == null || c.ExpiryDate >= DateTime.Today));
                if (coupon == null)
                {
                    ModelState.AddModelError(nameof(vm.CouponCode), "This promo code is invalid or has expired.");
                }
            }

            if (!ModelState.IsValid)
            {
                ViewBag.CartItems = cartItems;
                ViewBag.GrandTotal = grandTotal;
                ViewBag.DiscountAmount = 0m;
                return View(vm);
            }

            var discountAmount = coupon == null ? 0m : Math.Round(grandTotal * coupon.DiscountPercent / 100m, 2);
            var finalTotal = grandTotal - discountAmount;

            var order = new Order
            {
                MemberEmail = userEmail,
                OrderDate = DateTime.Now,
                Status = "Completed",
                TotalAmount = finalTotal,
                CouponCode = coupon?.Code,
                DiscountAmount = discountAmount
            };

            db.Orders.Add(order);
            db.SaveChanges();

            foreach (var item in cartItems)
            {
                int days = (item.CheckOutDate - item.CheckInDate).Days;
                if (days <= 0) days = 1;

                var orderItem = new OrderItem
                {
                    OrderId = order.OrderId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    CheckInDate = item.CheckInDate,
                    CheckOutDate = item.CheckOutDate,
                    Price = (decimal)days * item.Product.Price * item.Quantity
                };
                db.OrderItems.Add(orderItem);
            }

            // Remove cart items after successful order creation
            db.ShoppingCartItems.RemoveRange(cartItems);
            db.SaveChanges();

            TempData["Info"] = "Payment successful! Room reservation confirmed.";

            // FIX: Redirect to Details page after successful payment
            return RedirectToAction(nameof(Details), new { id = order.OrderId });
        }

        ViewBag.CartItems = cartItems;
        ViewBag.GrandTotal = cartItems.Sum(item =>
            (decimal)(item.CheckOutDate - item.CheckInDate).Days * item.Product.Price * item.Quantity
        );
        ViewBag.DiscountAmount = 0m;
        return View(vm);
    }

    // GET: Order/History (Personal)
    [Authorize]
    public IActionResult History()
    {
        string userEmail = User.Identity?.Name ?? "";

        var orders = db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Where(o => o.MemberEmail == userEmail)
            .OrderByDescending(o => o.OrderDate)
            .ToList();

        return View(orders);
    }

    // POST: Order/CancelOrder
    // Member can only cancel if check-in date has not been reached yet
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CancelOrder(int id)
    {
        string userEmail = User.Identity?.Name ?? "";

        var order = db.Orders
            .Include(o => o.Items)
            .FirstOrDefault(o => o.OrderId == id && o.MemberEmail == userEmail);

        if (order == null)
        {
            TempData["Error"] = "Order not found.";
            return RedirectToAction(nameof(History));
        }

        if (order.Status == "Cancelled")
        {
            TempData["Error"] = "This order is already cancelled.";
            return RedirectToAction(nameof(History));
        }

        // Check that none of the booked rooms have already started check-in
        bool checkInPassed = order.Items.Any(i => i.CheckInDate.Date <= DateTime.Today);

        if (checkInPassed)
        {
            TempData["Error"] = "This order cannot be cancelled because check-in date has already been reached.";
            return RedirectToAction(nameof(History));
        }

        order.Status = "Cancelled";
        db.SaveChanges();

        TempData["Info"] = $"Order #{order.OrderId} has been cancelled successfully.";
        return RedirectToAction(nameof(History));
    }

    // GET: Order/Details/5 (View the receipt for a specific order)
    [Authorize]
    public IActionResult Details(int id)
    {
        string userEmail = User.Identity?.Name ?? "";

        var order = db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefault(o => o.OrderId == id);

        if (order == null)
        {
            return RedirectToAction(nameof(History));
        }

        // Access check
        if (!User.IsInRole("Admin") && order.MemberEmail != userEmail)
        {
            return Forbid();
        }

        return View(order);
    }

    // GET: Order/Invoice/5 (Export PDF Receipt / Invoice View)
    [Authorize]
    public IActionResult Invoice(int id)
    {
        string userEmail = User.Identity?.Name ?? "";

        var order = db.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefault(o => o.OrderId == id);

        if (order == null)
        {
            return RedirectToAction(nameof(History));
        }

        // Access check: only admins or order owner can view
        if (!User.IsInRole("Admin") && order.MemberEmail != userEmail)
        {
            return Forbid();
        }

        return View(order);
    }

    // ========================================================================
    // FEATURE 4: Webcam QR Code Check-in Verification Handler (Admin Only)
    // ========================================================================
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public IActionResult CheckIn(int id)
    {
        var order = db.Orders.Find(id);

        if (order != null)
        {
            order.Status = "Completed";
            db.SaveChanges();

            TempData["Success"] = $"Order #{id} has been successfully checked in!";
        }
        else
        {
            TempData["Error"] = $"Order #{id} was not found in the database.";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Order/ProcessScan - called by the QR scanner page
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ProcessScan([FromBody] ScanRequest request)
    {
        if (request == null || request.OrderId <= 0)
            return Json(new { success = false, message = "Invalid QR code." });

        var order = db.Orders.FirstOrDefault(o => o.OrderId == request.OrderId);
        if (order == null)
            return Json(new { success = false, message = $"Order #{request.OrderId} was not found." });

        if (order.Status == "Cancelled")
            return Json(new { success = false, message = "This order has been cancelled." });

        order.Status = "Completed";
        db.SaveChanges();

        return Json(new
        {
            success = true,
            message = $"Order #{order.OrderId} checked in successfully.",
            redirectUrl = Url.Action(nameof(Details), new { id = order.OrderId })
        });
    }
    
    [Authorize(Roles = "Admin")]
    public IActionResult ScanQR()
    {
            return View();
    }

}

public sealed class ScanRequest
{
    public int OrderId { get; set; }
}
