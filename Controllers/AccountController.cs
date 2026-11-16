using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Net.Mail;

namespace Demo.Controllers;

public class AccountController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helper hp;

    public AccountController(DB db, IWebHostEnvironment en, Helper hp)
    {
        this.db = db;
        this.en = en;
        this.hp = hp;
    }

    // GET: Account/Login
    public IActionResult Login()
    {
        return View();
    }

    // POST: Account/Login
    [HttpPost]
    public IActionResult Login(LoginVM vm, string? returnURL)
    {
        // Additional Feature 1: Account Lockout Keys
        string lockoutKey = $"Lockout_{vm.Email}";
        string attemptsKey = $"FailedAttempts_{vm.Email}";

        // Additional Feature 1: Check if account is currently locked out
        var lockoutTimeStr = HttpContext.Session.GetString(lockoutKey);
        if (!string.IsNullOrEmpty(lockoutTimeStr))
        {
            DateTime lockoutEnd = DateTime.Parse(lockoutTimeStr);
            if (DateTime.Now < lockoutEnd)
            {
                var remainingMinutes = Math.Ceiling((lockoutEnd - DateTime.Now).TotalMinutes);
                ModelState.AddModelError("", $"Account is temporarily locked due to multiple failed login attempts. Please try again in {remainingMinutes} minute(s).");
                return View(vm);
            }
            else
            {
                // Lockout period expired, clear lock status from Session
                HttpContext.Session.Remove(lockoutKey);
                HttpContext.Session.Remove(attemptsKey);
            }
        }

        // Get user record based on email (PK)
        var u = db.Users.Find(vm.Email);

        // Custom validation -> verify password
        if (u == null || !hp.VerifyPassword(u.Hash, vm.Password))
        {
            // Additional Feature 1: Increment failed attempt counter in Session
            int attempts = (HttpContext.Session.GetInt32(attemptsKey) ?? 0) + 1;
            HttpContext.Session.SetInt32(attemptsKey, attempts);

            if (attempts >= 3)
            {
                // Lock account for 15 minutes after 3 failed attempts
                DateTime lockoutUntil = DateTime.Now.AddMinutes(15);
                HttpContext.Session.SetString(lockoutKey, lockoutUntil.ToString("o"));
                ModelState.AddModelError("", "Account has been locked for 15 minutes due to 3 consecutive failed login attempts.");
            }
            else
            {
                int remaining = 3 - attempts;
                ModelState.AddModelError("", $"Login credentials not matched. You have {remaining} attempt(s) remaining before lockout.");
            }
        }

        // Check if the member account has been blocked by admin
        if (u != null && u.IsBlocked)
        {
            ModelState.AddModelError("", "Your account has been suspended. Please contact support for assistance.");
        }

        if (ModelState.IsValid)
        {
            // Additional Feature 1: Login successful -> reset failure count and lock status
            HttpContext.Session.Remove(lockoutKey);
            HttpContext.Session.Remove(attemptsKey);

            // Sign in
            hp.SignIn(u!.Email, u.Role, vm.RememberMe);

            if (u.IsTemporaryPassword)
            {
                TempData["ShowTempPasswordModal"] = true;
            }

            // Handle return URL
            if (!string.IsNullOrEmpty(returnURL))
            {
                return Redirect(returnURL);
            }

            // Redirect to the home page matching the user's role
            // (u.Role is "Admin" or "Member", same as the Home action names)
            return RedirectToAction(u.Role, "Home");
        }

        return View(vm);
    }

    // GET: Account/Logout
    public IActionResult Logout(string? returnURL)
    {
        hp.SignOut();
        return RedirectToAction("Index", "Home");
    }

    // GET: Account/AccessDenied
    public IActionResult AccessDenied(string? returnURL)
    {
        return View();
    }

    // GET: Account/CheckEmail
    public bool CheckEmail(string email)
    {
        return !db.Users.Any(u => u.Email == email);
    }

    // GET: Account/Register
    public IActionResult Register()
    {
        return View();
    }

    // POST: Account/Register
    [HttpPost]
    public IActionResult Register(RegisterVM vm)
    {
        if (ModelState.IsValid("Email") &&
            db.Users.Any(u => u.Email == vm.Email))
        {
            ModelState.AddModelError("Email", "Duplicated Email.");
        }

        if (ModelState.IsValid("Photo"))
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }

        if (ModelState.IsValid)
        {
            // Insert member
            db.Members.Add(new()
            {
                Email = vm.Email,
                Hash = hp.HashPassword(vm.Password),
                Name = vm.Name,
                PhotoURL = hp.SavePhoto(vm.Photo, "photos"),
            });
            db.SaveChanges();

            TempData["Info"] = "Register successfully. Please login.";
            return RedirectToAction("Login");
        }

        return View(vm);
    }

    // GET: Account/UpdatePassword
    [Authorize(Roles = "Admin,Member")]
    public IActionResult UpdatePassword()
    {
        return View();
    }

    // POST: Account/UpdatePassword
    [HttpPost]
    [Authorize(Roles = "Admin,Member")]
    public IActionResult UpdatePassword(UpdatePasswordVM vm)
    {
        var u = db.Users.Find(User.Identity!.Name);
        if (u == null) return RedirectToAction("Index", "Home");

        if (!hp.VerifyPassword(u.Hash, vm.Current))
        {
            ModelState.AddModelError("Current", "Current Password not matched.");
        }

        if (ModelState.IsValid)
        {
            u.Hash = hp.HashPassword(vm.New);
            u.IsTemporaryPassword = false;
            db.SaveChanges();

            TempData["Info"] = "Password updated.";
            return RedirectToAction();
        }

        return View();
    }

    // GET: Account/UpdateProfile
    [Authorize(Roles = "Admin,Member")]
    public IActionResult UpdateProfile()
    {
        UpdateProfileVM vm;

        if (User.IsInRole("Admin"))
        {
            var a = db.Admins.Find(User.Identity!.Name);
            if (a == null) return RedirectToAction("Index", "Home");

            vm = new UpdateProfileVM
            {
                Email = a.Email,
                Name = a.Name,
                PhotoURL = a.PhotoURL,
            };
        }
        else
        {
            var m = db.Members.Find(User.Identity!.Name);
            if (m == null) return RedirectToAction("Index", "Home");

            vm = new UpdateProfileVM
            {
                Email = m.Email,
                Name = m.Name,
                PhotoURL = m.PhotoURL,
            };
        }

        return View(vm);
    }

    // POST: Account/UpdateProfile
    [HttpPost]
    [Authorize(Roles = "Admin,Member")]
    public IActionResult UpdateProfile(UpdateProfileVM vm)
    {
        if (vm.Photo != null)
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }

        if (User.IsInRole("Admin"))
        {
            var a = db.Admins.Find(User.Identity!.Name);
            if (a == null) return RedirectToAction("Index", "Home");

            if (ModelState.IsValid)
            {
                a.Name = vm.Name;

                if (vm.Photo != null)
                {
                    hp.DeletePhoto(a.PhotoURL, "photos");
                    a.PhotoURL = hp.SavePhoto(vm.Photo, "photos");
                }

                db.SaveChanges();

                TempData["Info"] = "Profile updated.";
                return RedirectToAction();
            }

            vm.Email = a.Email;
            vm.PhotoURL = a.PhotoURL;
        }
        else
        {
            var m = db.Members.Find(User.Identity!.Name);
            if (m == null) return RedirectToAction("Index", "Home");

            if (ModelState.IsValid)
            {
                m.Name = vm.Name;

                if (vm.Photo != null)
                {
                    hp.DeletePhoto(m.PhotoURL, "photos");
                    m.PhotoURL = hp.SavePhoto(vm.Photo, "photos");
                }

                db.SaveChanges();

                TempData["Info"] = "Profile updated.";
                return RedirectToAction();
            }

            vm.Email = m.Email;
            vm.PhotoURL = m.PhotoURL;
        }

        return View(vm);
    }

    // GET: Account/DeactivateAccount
    [Authorize(Roles = "Admin,Member")]
    public IActionResult DeactivateAccount()
    {
        return View();
    }

    // POST: Account/DeactivateAccount
    [HttpPost]
    [Authorize(Roles = "Admin,Member")]
    public IActionResult DeactivateAccount(DeactivateAccountVM vm)
    {
        var u = db.Users.Find(User.Identity!.Name);
        if (u == null) return RedirectToAction("Index", "Home");

        if (!hp.VerifyPassword(u.Hash, vm.Password))
        {
            ModelState.AddModelError("Password", "Password not matched.");
        }

        if (ModelState.IsValid && u is Admin && db.Admins.Count() <= 1)
        {
            ModelState.AddModelError("", "This is the last admin account and cannot be deactivated.");
        }

        if (ModelState.IsValid)
        {
            if (u.PhotoURL != null)
            {
                hp.DeletePhoto(u.PhotoURL, "photos");
            }

            db.Users.Remove(u);
            db.SaveChanges();

            hp.SignOut();

            TempData["Info"] = "Your account has been deactivated.";
            return RedirectToAction("Index", "Home");
        }

        return View(vm);
    }

    // GET: Account/ResetPassword
    public IActionResult ResetPassword()
    {
        var email = TempData["TempPasswordEmail"] as string;
        if (!string.IsNullOrEmpty(email))
        {
            ViewBag.Email = email;
        }

        return View();
    }

    // POST: Account/ResetPassword
    [HttpPost]
    public IActionResult ResetPassword(ResetPasswordVM vm)
    {
        var u = db.Users.Find(vm.Email);

        if (u == null)
        {
            ModelState.AddModelError("Email", "Email not found.");
        }

        if (ModelState.IsValid)
        {
            SendTemporaryPassword(u!.Email);

            TempData["TempPasswordEmail"] = vm.Email;
            return RedirectToAction();
        }

        return View();
    }

    // POST: Account/ResendTempPassword
    [HttpPost]
    public IActionResult ResendTempPassword(string email)
    {
        var u = db.Users.Find(email);

        if (u != null)
        {
            SendTemporaryPassword(u.Email);
            TempData["Info"] = "Temporary password resent to your email.";
        }

        TempData["TempPasswordEmail"] = email;
        return RedirectToAction("ResetPassword");
    }

    // Generates temporary password
    private void SendTemporaryPassword(string email)
    {
        var u = db.Users.Find(email);
        if (u == null) return;

        string password = hp.RandomPassword();
        u.Hash = hp.HashPassword(password);
        u.IsTemporaryPassword = true;
        db.SaveChanges();

        SendTemporaryPasswordEmail(u, password);
    }

    private void SendTemporaryPasswordEmail(User u, string password)
    {
        var mail = new MailMessage();
        mail.To.Add(new MailAddress(u.Email, u.Name));
        mail.Subject = "Your One-Time Temporary Password";
        mail.IsBodyHtml = true;

        var url = Url.Action("Login", "Account", null, "https");

        var path = string.IsNullOrEmpty(u.PhotoURL) ? "" : Path.Combine(en.WebRootPath, "photos", u.PhotoURL);
        Attachment? att = null;
        if (path != "" && System.IO.File.Exists(path))
        {
            att = new Attachment(path);
            mail.Attachments.Add(att);
            att.ContentId = "photo";
        }

        string photoTag = att != null
            ? "<img src='cid:photo' style='width: 120px; height: 120px; border: 1px solid #333; border-radius: 50%;'>"
            : "";

        mail.Body = $@"
            {photoTag}
            <p>Dear {u.Name},</p>
            <p>Here is your one-time temporary password:</p>
            <h1 style='letter-spacing: 2px'>{password}</h1>
            <p>
                Please <a href='{url}'>login</a>
                with this password, then set a new password.
            </p>
            <p style='color:#999;font-size:12px;'>If you did not request this, you can safely ignore this email.</p>
        ";

        hp.SendEmail(mail);
    }

    // POST: Account/SetTheme
    [HttpPost]
    [Authorize(Roles = "Admin,Member")]
    public IActionResult SetTheme(string theme)
    {
        if (theme != "Light" && theme != "Dark")
        {
            return BadRequest();
        }

        var u = db.Users.Find(User.Identity!.Name);
        if (u == null) return Unauthorized();

        u.Theme = theme;
        db.SaveChanges();

        return Ok();
    }
}
