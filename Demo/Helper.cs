using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Demo;

public class Helper
{
    private readonly IWebHostEnvironment en;
    private readonly IHttpContextAccessor ct;
    private readonly IConfiguration config;

    public Helper(IWebHostEnvironment en, IHttpContextAccessor ct, IConfiguration config)
    {
        this.en = en;
        this.ct = ct;
        this.config = config;
    }


    // ------------------------------------------------------------------------
    // Photo Upload
    // ------------------------------------------------------------------------

    public string ValidatePhoto(IFormFile? f)
    {
        if (f == null || f.Length == 0)
        {
            return "Please select a photo.";
        }

        var reType = new Regex(@"^image\/(jpeg|jpg|png|webp)$", RegexOptions.IgnoreCase);
        var reName = new Regex(@"^.+\.(jpeg|jpg|png|webp)$", RegexOptions.IgnoreCase);

        if (!reType.IsMatch(f.ContentType) || !reName.IsMatch(f.FileName))
        {
            return "Only JPG, PNG, or WEBP photos are allowed.";
        }
        else if (f.Length > 2 * 1024 * 1024) // Upward limit to 2MB for hotel photos
        {
            return "Photo size cannot be more than 2MB.";
        }

        return "";
    }

    public string SavePhoto(IFormFile? f, string folder)
    {
        if (f == null) return "default.jpg";

        var file = Guid.NewGuid().ToString("n") + ".jpg";

        // Ensure the folder exists in wwwroot/
        var folderPath = Path.Combine(en.WebRootPath, folder);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        var path = Path.Combine(folderPath, file);

        // Resize to high quality 800x600 maintaining aspect ratio (Fit mode instead of hard Crop)
        var options = new ResizeOptions
        {
            Size = new Size(800, 600),
            Mode = ResizeMode.Max // Resizes dynamically without cropping or squishing
        };

        using var stream = f.OpenReadStream();
        using var img = Image.Load(stream);
        img.Mutate(x => x.Resize(options));
        img.Save(path);

        return file;
    }

    public void DeletePhoto(string? file, string folder)
    {
        if (string.IsNullOrEmpty(file) || file == "default.jpg") return;

        file = Path.GetFileName(file);
        var path = Path.Combine(en.WebRootPath, folder, file);

        if (!File.Exists(path)) return;

        for (int i = 0; i < 3; i++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(200);
            }
        }
    }
    // ------------------------------------------------------------------------
    // Security Helper Functions
    // ------------------------------------------------------------------------

    private readonly PasswordHasher<object> ph = new();

    public string HashPassword(string password)
    {
        return ph.HashPassword(0, password);
    }

    public bool VerifyPassword(string hash, string password)
    {
        return ph.VerifyHashedPassword(0, hash, password)
            == PasswordVerificationResult.Success;
    }

    public void SignIn(string email, string role, bool rememberMe)
    {
        // Claim, identity and principal
        List<Claim> claims = [
            new(ClaimTypes.Name, email),
            new(ClaimTypes.Role, role),
            ];

        ClaimsIdentity identity = new(claims, "Cookies");

        ClaimsPrincipal principal = new(identity);

        // Remember me (authentication properties)
        AuthenticationProperties properties = new()
        {
            IsPersistent = rememberMe,
        };

        // Sign in
        ct.HttpContext!.SignInAsync(principal, properties);
    }

    public void SignOut()
    {
        // Sign out
        ct.HttpContext!.SignOutAsync();
    }

    public string RandomPassword()
    {
        string s = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string password = "";

        Random r = new();

        for (int i = 1; i <= 10; i++)
        {
            password += s[r.Next(s.Length)];
        }

        return password;
    }

    // ------------------------------------------------------------------------
    // Email Sending Helper
    // ------------------------------------------------------------------------

    public void SendEmail(MailMessage mail)
    {
        string user = config["Smtp:User"] ?? "";
        string pass = config["Smtp:Pass"] ?? "";
        string name = config["Smtp:Name"] ?? "";
        string host = config["Smtp:Host"] ?? "";
        int port = config.GetValue<int>("Smtp:Port");

        mail.From = new MailAddress(user, name);

        using var smtp = new SmtpClient
        {
            Host = host,
            Port = port,
            EnableSsl = true,
            Credentials = new NetworkCredential(user, pass),
        };

        smtp.Send(mail);
    }
}