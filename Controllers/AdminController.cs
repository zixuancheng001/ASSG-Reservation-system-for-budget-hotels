using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Demo.Models; // Change 'Demo' to your project's root namespace if different

namespace Demo.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly DB _db; // Replace DB with your actual DbContext class name
        private readonly IWebHostEnvironment _target;

        public AdminController(DB db, IWebHostEnvironment target)
        {
            _db = db;
            _target = target;
        }

        // GET: /Admin/Index
        public IActionResult Index()
        {
            var model = _db.Admins.ToList();
            return View(model);
        }

        // GET: /Admin/Add
        public IActionResult Add()
        {
            return View();
        }

        // POST: /Admin/Add
        [HttpPost]
        public IActionResult Add(RegisterVM vm)
        {
            // Email uniqueness validation
            if (_db.Admins.Any(a => a.Email == vm.Email))
            {
                ModelState.AddModelError("Email", "Email address already registered.");
            }

            if (ModelState.IsValid)
            {
                string photoFilename = "photo.jpg"; // Default placeholder photo

                if (vm.Photo != null && vm.Photo.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_target.WebRootPath, "photos");
                    Directory.CreateDirectory(uploadsFolder);

                    photoFilename = Guid.NewGuid().ToString("N") + Path.GetExtension(vm.Photo.FileName);
                    string filePath = Path.Combine(uploadsFolder, photoFilename);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        vm.Photo.CopyTo(stream);
                    }
                }

                var admin = new Admin
                {
                    Email = vm.Email,
                    Name = vm.Name,
                    Hash = SHA256(vm.Password),
                    PhotoURL = photoFilename
                };

                _db.Admins.Add(admin);
                _db.SaveChanges();

                TempData["Info"] = "New admin added successfully.";
                return RedirectToAction("Index");
            }

            return View(vm);
        }

        // GET: /Admin/Edit/{id}
        public IActionResult Edit(string id)
        {
            var admin = _db.Admins.Find(id);
            if (admin == null)
            {
                return NotFound();
            }

            var vm = new EditAdminVM
            {
                Email = admin.Email,
                Name = admin.Name,
                PhotoURL = admin.PhotoURL
            };

            return View(vm);
        }

        // POST: /Admin/Edit
        [HttpPost]
        public IActionResult Edit(EditAdminVM vm)
        {
            var admin = _db.Admins.Find(vm.Email);
            if (admin == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                // Only updates the Name field
                admin.Name = vm.Name;

                _db.SaveChanges();
                TempData["Info"] = "Admin updated successfully.";
                return RedirectToAction("Index");
            }

            return View(vm);
        }

        // POST: /Admin/Delete/{id}
        [HttpPost]
        public IActionResult Delete(string id)
        {
            // Prevent admin delete their own account
            if (string.Equals(id, User.Identity!.Name, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Info"] = "You cannot delete your own admin account while logged in.";
                return RedirectToAction("Index");
            }

            var admin = _db.Admins.Find(id);
            if (admin != null)
            {
                if (!string.IsNullOrEmpty(admin.PhotoURL) && admin.PhotoURL != "photo.jpg")
                {
                    string filePath = Path.Combine(_target.WebRootPath, "photos", admin.PhotoURL);
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }

                _db.Admins.Remove(admin);
                _db.SaveChanges();
                TempData["Info"] = "Admin deleted successfully.";
            }

            return RedirectToAction("Index");
        }

        // GET: /Admin/GetAdminsJson
        [HttpGet]
        public IActionResult GetAdminsJson(string? query, string? sortBy, int page = 1)
        {
            int pageSize = 5;
            var admins = _db.Admins.AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(query))
            {
                admins = admins.Where(a => a.Name.Contains(query) || a.Email.Contains(query));
            }

            // Sort
            admins = sortBy switch
            {
                "name_desc" => admins.OrderByDescending(a => a.Name),
                "email_asc" => admins.OrderBy(a => a.Email),
                "email_desc" => admins.OrderByDescending(a => a.Email),
                _ => admins.OrderBy(a => a.Name), // name_asc (default)
            };

            // Pagination
            int totalItems = admins.Count();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            page = Math.Clamp(page, 1, Math.Max(1, totalPages));

            var pagedData = admins
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Email,
                    a.Name,
                    a.PhotoURL
                })
                .ToList();

            return Json(new
            {
                items = pagedData,
                currentPage = page,
                totalPages = totalPages,
                totalItems = totalItems
            });
        }

        private string SHA256(string input)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(input);
                byte[] hash = sha.ComputeHash(bytes);
                return Convert.ToHexString(hash);
            }
        }
    }
}