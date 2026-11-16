using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers;

public class HomeController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helper hp;

    public HomeController(DB db, IWebHostEnvironment en, Helper hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
    }

    // GET: Home/Index
    public IActionResult Index()
    {
        return View();
    }

    // GET: Home/Member
    [Authorize(Roles = "Member")]
    public IActionResult Member()
    {
        var email = User.Identity!.Name!;

        // Available room count for stats card
        ViewBag.FeaturedRooms = db.Products
            .Where(p => p.IsAvailable && !p.IsMaintenance)
            .ToList();

        // Member's recent orders (up to 3)
        try
        {
            ViewBag.RecentOrders = db.Orders
                .Where(o => o.MemberEmail == email)
                .OrderByDescending(o => o.OrderDate)
                .Take(3)
                .ToList();

            ViewBag.TotalOrders = db.Orders
                .Count(o => o.MemberEmail == email);
        }
        catch
        {
            ViewBag.RecentOrders = new List<Order>();
            ViewBag.TotalOrders  = 0;
        }

        // Cart item count
        var cart = db.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefault(c => c.MemberEmail == email);
        ViewBag.CartCount = cart?.Items.Count ?? 0;

        return View();
    }

    // GET: Home/Admin
    [Authorize(Roles = "Admin")]
    public IActionResult Admin()
    {
        ViewBag.CategoryCount = db.Categories.Count();
        ViewBag.ProductCount  = db.Products.Count();
        ViewBag.MemberCount   = db.Members.Count();
        ViewBag.AdminCount    = db.Admins.Count();

        // Orders table is created via EnsureOrderTables (raw SQL), check before querying
        try
        {
            ViewBag.OrderCount   = db.Orders.Count();
            ViewBag.PendingCount = db.Orders.Count(o => o.Status == "Pending");
            ViewBag.Revenue      = db.Orders
                .Where(o => o.Status == "Completed")
                .Sum(o => (decimal?)o.TotalAmount) ?? 0;

            // Chart 1: Order status distribution (pie)
            ViewBag.StatusCompleted = db.Orders.Count(o => o.Status == "Completed");
            ViewBag.StatusCancelled = db.Orders.Count(o => o.Status == "Cancelled");
            ViewBag.StatusPending   = db.Orders.Count(o => o.Status == "Pending");

            // Chart 2: Monthly revenue for last 6 months (bar)
            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);
            var monthlyRevenue = db.Orders
                .Where(o => o.Status == "Completed" &&
                            o.OrderDate >= monthStart)
                .AsEnumerable()
                .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                .ToDictionary(
                    g => new DateTime(g.Key.Year, g.Key.Month, 1),
                    g => g.Sum(o => o.TotalAmount));

            var revenueLabels = new List<string>();
            var revenueData   = new List<decimal>();

            for (int i = 0; i < 6; i++)
            {
                var month = monthStart.AddMonths(i);
                revenueLabels.Add(month.ToString("MMM yyyy"));
                revenueData.Add(monthlyRevenue.TryGetValue(month, out var amount) ? amount : 0m);
            }

            ViewBag.RevenueLabels = revenueLabels;
            ViewBag.RevenueData   = revenueData;

            // Chart 3: New member registrations — use Members count per month (last 6)
            // Members don't store CreatedAt, so we use order history as proxy
            // Instead show top 5 rooms by order count
            var topRooms = db.OrderItems
                .GroupBy(i => i.ProductId)
                .Select(g => new
                {
                    RoomId = g.Key,
                    Count  = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToList();

            // Resolve names
            var roomIds   = topRooms.Select(r => r.RoomId).ToList();
            var roomNames = db.Products
                .Where(p => roomIds.Contains(p.ProductId))
                .ToDictionary(p => p.ProductId, p => p.Name);

            ViewBag.TopRoomLabels = topRooms
                .Select(r => roomNames.ContainsKey(r.RoomId) ? roomNames[r.RoomId] : r.RoomId)
                .ToList();
            ViewBag.TopRoomData = topRooms.Select(r => r.Count).ToList();
        }
        catch
        {
            ViewBag.OrderCount      = 0;
            ViewBag.PendingCount    = 0;
            ViewBag.Revenue         = 0m;
            ViewBag.StatusCompleted = 0;
            ViewBag.StatusCancelled = 0;
            ViewBag.StatusPending   = 0;
            ViewBag.RevenueLabels   = new List<string>();
            ViewBag.RevenueData     = new List<decimal>();
            ViewBag.TopRoomLabels   = new List<string>();
            ViewBag.TopRoomData     = new List<int>();
        }

        return View();
    }
}
