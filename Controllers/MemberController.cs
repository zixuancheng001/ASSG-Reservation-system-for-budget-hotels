using Demo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers;

[Authorize(Roles = "Admin")]
public class MemberController : Controller
{
    private readonly DB db;
    private readonly Helper hp;

    public MemberController(DB db, Helper hp)
    {
        this.db = db;
        this.hp = hp;
    }

    // ------------------------------------------------------------------------
    // GET: Member/Index
    // ------------------------------------------------------------------------
    public IActionResult Index()
    {
        return View();
    }

    // ------------------------------------------------------------------------
    // GET: Member/GetMembersJson  (AJAX endpoint)
    // ------------------------------------------------------------------------
    [HttpGet]
    public IActionResult GetMembersJson(string? query, string? sortBy, int page = 1)
    {
        int pageSize = 5;
        page = Math.Max(page, 1);

        var membersQuery = db.Members.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            membersQuery = membersQuery.Where(m =>
                m.Name.Contains(query) ||
                m.Email.Contains(query));
        }

        membersQuery = sortBy switch
        {
            "name_desc"  => membersQuery.OrderByDescending(m => m.Name),
            "email_asc"  => membersQuery.OrderBy(m => m.Email),
            "email_desc" => membersQuery.OrderByDescending(m => m.Email),
            _            => membersQuery.OrderBy(m => m.Name)
        };

        int totalItems = membersQuery.Count();
        int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        if (totalPages > 0 && page > totalPages) page = totalPages;

        var items = membersQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                email = m.Email,
                name = m.Name,
                photoURL = string.IsNullOrEmpty(m.PhotoURL) ? "photo.jpg" : m.PhotoURL,
                isBlocked = m.IsBlocked
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

    // ------------------------------------------------------------------------
    // POST: Member/ToggleBlock
    // ------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleBlock(string id)
    {
        var m = db.Members.Find(id);

        if (m == null)
        {
            return Json(new { success = false, message = "Member not found." });
        }

        m.IsBlocked = !m.IsBlocked;
        db.SaveChanges();

        return Json(new { success = true, isBlocked = m.IsBlocked });
    }

    // ------------------------------------------------------------------------
    // GET: Member/Edit
    // ------------------------------------------------------------------------
    public IActionResult Edit(string id)
    {
        var m = db.Members.Find(id);
        if (m == null) return RedirectToAction(nameof(Index));

        var vm = new MemberVM
        {
            Email   = m.Email,
            Name    = m.Name,
            PhotoURL = m.PhotoURL,
        };

        return View(vm);
    }

    // ------------------------------------------------------------------------
    // POST: Member/Edit
    // ------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(MemberVM vm)
    {
        var m = db.Members.Find(vm.Email);
        if (m == null) return RedirectToAction(nameof(Index));

        ModelState.Remove(nameof(vm.PhotoURL));
        ModelState.Remove(nameof(vm.Email));

        if (vm.Photo != null)
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (!string.IsNullOrEmpty(err))
            {
                ModelState.AddModelError("Photo", err);
            }
        }
        else
        {
            ModelState.Remove(nameof(vm.Photo));
        }

        if (ModelState.IsValid)
        {
            m.Name = vm.Name;

            if (vm.Photo != null)
            {
                hp.DeletePhoto(m.PhotoURL, "photos");
                m.PhotoURL = hp.SavePhoto(vm.Photo, "photos");
            }

            db.SaveChanges();

            TempData["Info"] = "Member updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        vm.PhotoURL = m.PhotoURL;
        return View(vm);
    }

    // ------------------------------------------------------------------------
    // POST: Member/Delete
    // ------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(string id)
    {
        var m = db.Members.Find(id);

        if (m != null)
        {
            hp.DeletePhoto(m.PhotoURL, "photos");
            db.Members.Remove(m);
            db.SaveChanges();

            TempData["Info"] = "Member deleted successfully.";
        }

        return RedirectToAction(nameof(Index));
    }
}
