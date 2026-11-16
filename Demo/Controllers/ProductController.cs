using Demo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Demo.Controllers;

[Authorize(Roles = "Admin")]
public class ProductController : Controller
{
    private readonly DB db;
    private readonly Helper hp;

    public ProductController(DB db, Helper hp)
    {
        this.db = db;
        this.hp = hp;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.CategoryList = new SelectList(db.Categories, "CategoryId", "Name");
        var products = db.Products.Include(p => p.Category).ToList();
        return View(products);
    }

    // ----------------------------------------------------
    // EDIT 1: Include amenities in GetProductsJson
    // ----------------------------------------------------
    [HttpGet]
    public IActionResult GetProductsJson(string? query, string? categoryId, string? status, string? sortBy, int page = 1)
    {
        int pageSize = 5;

        var productsQuery = db.Products.Include(p => p.Category).AsQueryable();
        page = Math.Max(page, 1);

        if (!string.IsNullOrWhiteSpace(query))
        {
            productsQuery = productsQuery.Where(p =>
                p.Name.Contains(query) ||
                p.ProductId.Contains(query));
        }

        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            productsQuery = productsQuery.Where(p => p.CategoryId == categoryId);
        }

        productsQuery = status switch
        {
            "available"   => productsQuery.Where(p => p.IsAvailable && !p.IsMaintenance),
            "booked"      => productsQuery.Where(p => !p.IsAvailable && !p.IsMaintenance),
            "maintenance" => productsQuery.Where(p => p.IsMaintenance),
            _             => productsQuery
        };

        productsQuery = sortBy switch
        {
            "name_desc" => productsQuery.OrderByDescending(p => p.Name),
            "price_asc" => productsQuery.OrderBy(p => p.Price),
            "price_desc" => productsQuery.OrderByDescending(p => p.Price),
            _ => productsQuery.OrderBy(p => p.Name)
        };

        int totalItems = productsQuery.Count();
        int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        if (totalPages > 0 && page > totalPages) page = totalPages;

        var items = productsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                productId = p.ProductId,
                name = p.Name,
                price = p.Price,
                capacity = p.Capacity,
                isAvailable = p.IsAvailable,
                isMaintenance = p.IsMaintenance,
                amenities = p.Amenities, // <-- ADDED THIS LINE
                categoryName = p.Category != null ? p.Category.Name : p.CategoryId,
                photoURL = string.IsNullOrEmpty(p.PhotoURL) ? "photo.jpg" : p.PhotoURL
            })
            .ToList();

        return Json(new
        {
            items = items,
            currentPage = page,
            totalPages = totalPages,
            totalItems = totalItems
        });
    }

    [HttpPost]
    public IActionResult ToggleAvailability(string id)
    {
        var product = db.Products.Find(id);
        if (product == null)
        {
            return Json(new { success = false, message = "Room not found." });
        }

        product.IsAvailable = !product.IsAvailable;
        db.SaveChanges();

        return Json(new { success = true, isAvailable = product.IsAvailable });
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewBag.CategoryList = new SelectList(db.Categories, "CategoryId", "Name");
        return View(new ProductVM());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ProductVM vm)
    {
        if (string.IsNullOrEmpty(vm.ProductId))
        {
            int nextNum = db.Products.Count() + 101;
            vm.ProductId = $"ROOM{nextNum}";
        }

        if (vm.Photo != null)
        {
            string photoError = hp.ValidatePhoto(vm.Photo);
            if (!string.IsNullOrEmpty(photoError))
            {
                ModelState.AddModelError("Photo", photoError);
            }
        }
        else
        {
            ModelState.AddModelError("Photo", "Please upload a room photo.");
        }

        ModelState.Remove(nameof(vm.ProductId));
        ModelState.Remove(nameof(vm.PhotoURL));

        if (ModelState.IsValid)
        {
            string amenitiesString = vm.SelectedAmenities != null ? string.Join(",", vm.SelectedAmenities) : "";

            var product = new Product
            {
                ProductId = vm.ProductId,
                Name = vm.Name,
                Price = vm.Price,
                Capacity = vm.Capacity,
                IsAvailable = vm.IsAvailable,
                IsMaintenance = vm.IsMaintenance,
                Amenities = amenitiesString,
                CategoryId = vm.CategoryId,
                PhotoURL = hp.SavePhoto(vm.Photo, "photos")
            };

            db.Products.Add(product);
            db.SaveChanges();

            TempData["Info"] = "Room created successfully!";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.CategoryList = new SelectList(db.Categories, "CategoryId", "Name", vm.CategoryId);
        return View(vm);
    }

    // ----------------------------------------------------
    // EDIT 2: Map IsMaintenance & Amenities in GET Edit
    // ----------------------------------------------------
    [HttpGet]
    public IActionResult Edit(string id)
    {
        var p = db.Products.Find(id);
        if (p == null) return RedirectToAction(nameof(Index));

        var vm = new ProductVM
        {
            ProductId = p.ProductId,
            Name = p.Name,
            Price = p.Price,
            Capacity = p.Capacity,
            IsAvailable = p.IsAvailable,
            IsMaintenance = p.IsMaintenance, // <-- ADDED THIS LINE
            CategoryId = p.CategoryId,
            PhotoURL = p.PhotoURL,
            // Convert database string "Wifi,Aircon" to List<string> for form checkboxes
            SelectedAmenities = !string.IsNullOrWhiteSpace(p.Amenities)
                ? p.Amenities.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                : new List<string>() // <-- ADDED THIS BLOCK
        };

        ViewBag.CategoryList = new SelectList(db.Categories, "CategoryId", "Name", p.CategoryId);
        return View(vm);
    }

    // ----------------------------------------------------
    // EDIT 3: Save IsMaintenance & Amenities in POST Edit
    // ----------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(ProductVM vm)
    {
        var p = db.Products.Find(vm.ProductId);
        if (p == null) return RedirectToAction(nameof(Index));

        ModelState.Remove(nameof(vm.PhotoURL));

        if (vm.Photo == null)
        {
            ModelState.Remove(nameof(vm.Photo));
        }
        else
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (!string.IsNullOrEmpty(err))
            {
                ModelState.AddModelError("Photo", err);
            }
        }

        if (ModelState.IsValid)
        {
            p.Name = vm.Name;
            p.Price = vm.Price;
            p.Capacity = vm.Capacity;
            p.IsAvailable = vm.IsAvailable;
            p.CategoryId = vm.CategoryId;

            // Save maintenance status & amenities to database
            p.IsMaintenance = vm.IsMaintenance; // <-- ADDED THIS LINE
            p.Amenities = vm.SelectedAmenities != null ? string.Join(",", vm.SelectedAmenities) : ""; // <-- ADDED THIS LINE

            if (vm.Photo != null)
            {
                if (!string.IsNullOrEmpty(p.PhotoURL))
                {
                    hp.DeletePhoto(p.PhotoURL, "photos");
                }
                p.PhotoURL = hp.SavePhoto(vm.Photo, "photos");
            }

            db.SaveChanges();

            TempData["Info"] = "Product/Room updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        vm.PhotoURL = p.PhotoURL;
        ViewBag.CategoryList = new SelectList(db.Categories, "CategoryId", "Name", vm.CategoryId);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(string id)
    {
        var p = db.Products.Find(id);

        if (p != null)
        {
            if (!string.IsNullOrEmpty(p.PhotoURL))
            {
                hp.DeletePhoto(p.PhotoURL, "photos");
            }

            db.Products.Remove(p);
            db.SaveChanges();

            TempData["Info"] = "Product/Room deleted successfully.";
        }

        return RedirectToAction(nameof(Index));
    }

    public bool CheckProductId(string productId)
    {
        return !db.Products.Any(p => p.ProductId == productId);
    }
}
