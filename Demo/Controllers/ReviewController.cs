using Demo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers;

[Authorize]
public class ReviewController(DB db) : Controller
{
    [Authorize(Roles = "Admin")]
    public IActionResult Index(string? search, int? rating)
    {
        var query = db.RoomReviews.Include(r => r.Product).Include(r => r.Member).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(r => r.ProductId.Contains(search) ||
                (r.Product != null && r.Product.Name.Contains(search)) ||
                r.MemberEmail.Contains(search));
        if (rating is >= 1 and <= 5)
            query = query.Where(r => r.Rating == rating.Value);

        ViewBag.Search = search;
        ViewBag.Rating = rating;
        return View(query.OrderByDescending(r => r.CreatedAt).ToList());
    }

    [Authorize(Roles = "Admin")]
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var review = db.RoomReviews.Find(id);
        if (review != null)
        {
            db.RoomReviews.Remove(review);
            db.SaveChanges();
            TempData["Info"] = "Review deleted successfully.";
        }
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Member")]
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Save(string productId, int rating, string comment)
    {
        var email = User.Identity?.Name!;
        var hasCompletedStay = db.OrderItems.Any(i => i.ProductId == productId &&
            i.Order.MemberEmail == email && i.Order.Status == "Completed" && i.CheckOutDate.Date <= DateTime.Today);

        if (!hasCompletedStay)
        {
            TempData["Error"] = "Only members who have completed a stay can leave a review.";
            return RedirectToAction("Details", "Catalog", new { id = productId });
        }
        if (rating is < 1 or > 5 || string.IsNullOrWhiteSpace(comment) || comment.Length > 500)
        {
            TempData["Error"] = "Please provide a rating from 1 to 5 and a review of up to 500 characters.";
            return RedirectToAction("Details", "Catalog", new { id = productId });
        }

        var review = db.RoomReviews.FirstOrDefault(r => r.ProductId == productId && r.MemberEmail == email);
        if (review == null)
        {
            db.RoomReviews.Add(new RoomReview { ProductId = productId, MemberEmail = email, Rating = rating, Comment = comment.Trim() });
            TempData["Info"] = "Thank you for your review.";
        }
        else
        {
            review.Rating = rating;
            review.Comment = comment.Trim();
            review.CreatedAt = DateTime.Now;
            TempData["Info"] = "Your review has been updated.";
        }
        db.SaveChanges();
        return RedirectToAction("Details", "Catalog", new { id = productId });
    }
}
