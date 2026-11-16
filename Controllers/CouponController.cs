using Demo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers;

[Authorize(Roles = "Admin")]
public class CouponController : Controller
{
    private readonly DB db;

    public CouponController(DB db)
    {
        this.db = db;
    }

    // GET: Coupon/Index
    public IActionResult Index()
    {
        var model = db.Coupons.OrderBy(c => c.Code).ToList();
        return View(model);
    }

    // GET: Coupon/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Coupon/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Coupon vm)
    {
        // Force code to uppercase to match how ApplyCoupon validates it
        if (!string.IsNullOrWhiteSpace(vm.Code))
        {
            vm.Code = vm.Code.Trim().ToUpperInvariant();
        }

        // Check for duplicate coupon code
        if (db.Coupons.Any(c => c.Code == vm.Code))
        {
            ModelState.AddModelError("Code", "This coupon code already exists.");
        }

        if (ModelState.IsValid)
        {
            db.Coupons.Add(vm);
            db.SaveChanges();

            TempData["Info"] = "Coupon created successfully.";
            return RedirectToAction(nameof(Index));
        }

        return View(vm);
    }

    // GET: Coupon/Edit/{id}
    public IActionResult Edit(string id)
    {
        var c = db.Coupons.Find(id);
        if (c == null) return RedirectToAction(nameof(Index));

        return View(c);
    }

    // POST: Coupon/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(Coupon vm)
    {
        var c = db.Coupons.Find(vm.Code);
        if (c == null) return RedirectToAction(nameof(Index));

        // Code is the PK and comes from hidden field, skip its validation
        ModelState.Remove(nameof(vm.Code));

        if (ModelState.IsValid)
        {
            c.DiscountPercent = vm.DiscountPercent;
            c.ExpiryDate      = vm.ExpiryDate;
            c.IsActive        = vm.IsActive;
            db.SaveChanges();

            TempData["Info"] = "Coupon updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        return View(vm);
    }

    // POST: Coupon/ToggleActive
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleActive(string id)
    {
        var c = db.Coupons.Find(id);

        if (c != null)
        {
            c.IsActive = !c.IsActive;
            db.SaveChanges();
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Coupon/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(string id)
    {
        var c = db.Coupons.Find(id);

        if (c != null)
        {
            db.Coupons.Remove(c);
            db.SaveChanges();
            TempData["Info"] = "Coupon deleted.";
        }

        return RedirectToAction(nameof(Index));
    }
}
