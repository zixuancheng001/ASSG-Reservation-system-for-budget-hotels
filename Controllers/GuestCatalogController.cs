using Demo.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Demo.Controllers;

public class GuestCatalogController : Controller
{
    private readonly DB db;

    public GuestCatalogController(DB db)
    {
        this.db = db;
    }

    // GET: GuestCatalog/Index
    public IActionResult Index(string? search, string? categoryId)
    {
        var query = db.Products
            .Include(p => p.Category)
            .AsQueryable();

        // Search by room name
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

        ViewBag.Search     = search;
        ViewBag.CategoryId = categoryId;

        ViewBag.Categories = db.Categories
            .OrderBy(c => c.Name)
            .ToList();

        return View(products);
    }

    // GET: GuestCatalog/Details/ROOM101
    public IActionResult Details(string id)
    {
        var product = db.Products
            .Include(p => p.Category)
            .FirstOrDefault(p => p.ProductId == id);

        if (product == null)
        {
            return RedirectToAction(nameof(Index));
        }

        return View(product);
    }
}
