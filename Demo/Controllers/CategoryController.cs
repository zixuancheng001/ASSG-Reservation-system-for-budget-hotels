using Demo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers;

[Authorize(Roles = "Admin")]
public class CategoryController : Controller
{
    private readonly DB db;
    private readonly Helper hp;

    public CategoryController(DB db, Helper hp)
    {
        this.db = db;
        this.hp = hp;
    }

    // GET: Category/Index
    public IActionResult Index()
    {
        var model = db.Categories.ToList();
        return View(model);
    }

    // GET: Category/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Category/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(CategoryVM model)
    {
        // 1. Auto-generate Category ID (e.g., CAT01, CAT02...)
        int count = db.Categories.Count() + 1;
        model.CategoryId = $"CAT{count:D2}";

        // Ensure duplicate prevention in case records were deleted
        while (db.Categories.Any(c => c.CategoryId == model.CategoryId))
        {
            count++;
            model.CategoryId = $"CAT-{count:D2}";
        }

        // 2. Remove CategoryId validation error from ModelState
        ModelState.Remove(nameof(model.CategoryId));

        // 3. Now check validity
        if (ModelState.IsValid)
        {
            var category = new Category
            {
                CategoryId = model.CategoryId,
                Name = model.Name,
                Description = model.Description,
                IsFeatured = model.IsFeatured // <-- Save IsFeatured property
            };

            db.Categories.Add(category);
            db.SaveChanges();

            TempData["Info"] = $"Category ({model.CategoryId}) created successfully.";
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    // GET: Category/Edit
    public IActionResult Edit(string id)
    {
        var c = db.Categories.Find(id);
        if (c == null) return RedirectToAction("Index");

        var vm = new CategoryVM
        {
            CategoryId = c.CategoryId,
            Name = c.Name,
            Description = c.Description,
            IsFeatured = c.IsFeatured // <-- Populate IsFeatured for form binding
        };

        return View(vm);
    }

    // POST: Category/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(CategoryVM vm)
    {
        var c = db.Categories.Find(vm.CategoryId);
        if (c == null) return RedirectToAction("Index");

        if (ModelState.IsValid)
        {
            c.Name = vm.Name;
            c.Description = vm.Description;
            c.IsFeatured = vm.IsFeatured; // <-- Save updated IsFeatured property

            db.SaveChanges();

            TempData["Info"] = "Category updated successfully.";
            return RedirectToAction("Index");
        }

        return View(vm);
    }

    // POST: Category/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(string id)
    {
        var c = db.Categories.Find(id);

        if (c != null)
        {
            if (db.Products.Any(p => p.CategoryId == id))
            {
                TempData["Info"] = "Cannot delete category because it contains products/rooms.";
                return RedirectToAction("Index");
            }

            db.Categories.Remove(c);
            db.SaveChanges();

            TempData["Info"] = "Category deleted successfully.";
        }

        return RedirectToAction("Index");
    }

    // GET: Category/GetCategoriesJson (AJAX Provider)
    [HttpGet]
    public IActionResult GetCategoriesJson(string query, string sortBy, int page = 1)
    {
        int pageSize = 5;
        var categories = db.Categories.AsQueryable();

        // 1. Search
        if (!string.IsNullOrEmpty(query))
        {
            categories = categories.Where(c => c.Name.Contains(query) || c.CategoryId.Contains(query));
        }

        // 2. Sort
        categories = sortBy switch
        {
            "name_desc" => categories.OrderByDescending(c => c.Name),
            "id_asc" => categories.OrderBy(c => c.CategoryId),
            "id_desc" => categories.OrderByDescending(c => c.CategoryId),
            _ => categories.OrderBy(c => c.Name),
        };

        // 3. Pagination
        int totalItems = categories.Count();
        int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Clamp(page, 1, Math.Max(1, totalPages));

        var pagedData = categories
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.CategoryId,
                c.Name,
                c.Description,
                isFeatured = c.IsFeatured, // <-- Project IsFeatured to JSON
                ProductCount = c.Products.Count()
            })
            .ToList();

        return Json(new
        {
            items = pagedData,
            currentPage = page,
            totalPages = totalPages
        });
    }

    // AJAX Check Duplicate CategoryId
    public bool CheckCategoryId(string categoryId)
    {
        return !db.Categories.Any(c => c.CategoryId == categoryId);
    }
}