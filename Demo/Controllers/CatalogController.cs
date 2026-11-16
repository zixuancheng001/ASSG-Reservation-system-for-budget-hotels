using Demo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace Demo.Controllers;

[Authorize(Roles = "Member")]
public class CatalogController : Controller
{
    private readonly DB db;

    public CatalogController(DB db)
    {
        this.db = db;
    }

    // GET: Catalog/Index
    public IActionResult Index(string? search, string? categoryId)
    {
        var query = db.Products
            .Include(p => p.Category)
            .AsQueryable();

        // Search by product/room name
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p =>
                p.Name.Contains(search));
        }

        // Filter by category
        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            query = query.Where(p =>
                p.CategoryId == categoryId);
        }

        var products = query
            .OrderBy(p => p.Name)
            .ToList();

        ViewBag.Search = search;
        ViewBag.CategoryId = categoryId;

        ViewBag.Categories = db.Categories
            .OrderBy(c => c.Name)
            .ToList();

        return View(products);
    }

    // GET: Catalog/Details/ROOM101
    public IActionResult Details(string id)
    {
        var product = db.Products
            .Include(p => p.Category)
            .FirstOrDefault(p => p.ProductId == id);

        if (product == null)
        {
            return RedirectToAction(nameof(Index));
        }

        var email = User.Identity?.Name;
        var reviews = db.RoomReviews
            .Include(r => r.Member)
            .Where(r => r.ProductId == id)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        ViewBag.Reviews = reviews;
        ViewBag.AverageRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0d;
        ViewBag.IsWishlisted = !string.IsNullOrEmpty(email) &&
            db.WishlistItems.Any(w => w.MemberEmail == email && w.ProductId == id);
        ViewBag.CanReview = !string.IsNullOrEmpty(email) && db.OrderItems.Any(i =>
            i.ProductId == id && i.Order.MemberEmail == email &&
            i.Order.Status == "Completed" && i.CheckOutDate.Date <= DateTime.Today);
        ViewBag.MyReview = !string.IsNullOrEmpty(email)
            ? reviews.FirstOrDefault(r => r.MemberEmail == email) : null;

        return View(product);
    }

    // GET: Catalog/Availability
    [HttpGet]
    public IActionResult Availability(int? year, int? month)
    {
        DateTime today = DateTime.Today;

        int selectedYear = year ?? today.Year;
        int selectedMonth = month ?? today.Month;

        // Make sure the selected month is valid
        if (selectedMonth < 1)
        {
            selectedMonth = 12;
            selectedYear--;
        }

        if (selectedMonth > 12)
        {
            selectedMonth = 1;
            selectedYear++;
        }

        DateTime monthStart =
            new DateTime(selectedYear, selectedMonth, 1);

        DateTime monthEnd =
            monthStart.AddMonths(1);

        // Get all rooms
        var products = db.Products
            .OrderBy(p => p.ProductId)
            .ToList();

        // Fetch confirmed orders that overlap this month (excluding cancelled orders)
        var confirmedBookings = db.OrderItems
            .Include(oi => oi.Order)
            .Where(oi => oi.Order.Status != "Cancelled" &&
                        oi.CheckInDate < monthEnd &&
                        oi.CheckOutDate > monthStart)
            .Select(oi => new ShoppingCartItem
            {
                ProductId = oi.ProductId,
                CheckInDate = oi.CheckInDate,
                CheckOutDate = oi.CheckOutDate
            })
            .ToList();

        // Fetch active cart items that overlap this month
        var cartBookings = db.ShoppingCartItems
            .Where(i =>
                i.CheckInDate < monthEnd &&
                i.CheckOutDate > monthStart)
            .ToList();

        // Combine both confirmed orders and cart reservations
        var bookings = confirmedBookings.Concat(cartBookings).ToList();

        var vm = new AvailabilityVM
        {
            Year = selectedYear,
            Month = selectedMonth,
            Products = products,
            Bookings = bookings
        };

        return View(vm);
    }
}
