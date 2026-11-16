using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Demo.Models;

// View Models ----------------------------------------------------------------

#nullable disable warnings

public class LoginVM
{
    [StringLength(100)]
    [EmailAddress]
    public string Email { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    public bool RememberMe { get; set; }
}

public class RegisterVM
{
    [Required]
    [StringLength(100)]
    [EmailAddress(ErrorMessage = "Invalid Email format.")]
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[a-zA-Z]{2,}$", ErrorMessage = "Invalid Email format. Please include a domain suffix, e.g. .com")]
    [Remote("CheckEmail", "Account", ErrorMessage = "Duplicated {0}.")]
    public string Email { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [Compare("Password")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    public string Confirm { get; set; }

    [StringLength(100)]
    public string Name { get; set; }

    public IFormFile? Photo { get; set; }

   
}

public class UpdatePasswordVM
{
    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string Current { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string New { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [Compare("New")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    public string Confirm { get; set; }
}

public class UpdateProfileVM
{
    public string? Email { get; set; }

    [StringLength(100)]
    public string Name { get; set; }

    public string? PhotoURL { get; set; }

    public IFormFile? Photo { get; set; }
}

public class ResetPasswordVM
{
    [StringLength(100)]
    [EmailAddress]
    public string Email { get; set; }
}

public class DeactivateAccountVM
{
    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    public string Password { get; set; }
}

// Category & Product View Models --------------------------------------------

public class CategoryVM
{
    [Required]
    [StringLength(10)]
    [Remote("CheckCategoryId", "Category", ErrorMessage = "Duplicated {0}.")]
    [Display(Name = "Category ID")]
    public string CategoryId { get; set; }

    [Required]
    [StringLength(100)]
    [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Category Name can only contain letters and spaces.")]
    [Display(Name = "Category Name")]
    public string Name { get; set; }

    [StringLength(255)]
    [RegularExpression(@"^[a-zA-Z\s]*$", ErrorMessage = "Description can only contain letters and spaces.")]
    public string Description { get; set; }

    [Display(Name = "Feature on Home Page")]
    public bool IsFeatured { get; set; }
}

public class ProductVM
{
    [Required]
    [StringLength(10)]
    [Remote("CheckProductId", "Product", ErrorMessage = "Duplicated {0}.")]
    [Display(Name = "Product / Room ID")]
    public string ProductId { get; set; }

    [Required(ErrorMessage = "Room Name / Number is required.")]
    [StringLength(100)]
    [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Room Name can only contain letters and spaces.")]
    [Display(Name = "Room Name / Number")]
    public string Name { get; set; }

    [Required]
    [Range(10.00, 2000.00, ErrorMessage = "Price must be between RM 10.00 and RM 2000.00.")]
    [Display(Name = "Price Per Night (RM)")]
    public decimal Price { get; set; }

    [Required]
    [Range(1, 10, ErrorMessage = "Capacity must be between 1 and 10 guests.")]
    public int Capacity { get; set; }

    [Display(Name = "Is Available")]
    public bool IsAvailable { get; set; } = true;

    [Required(ErrorMessage = "Please select a Category.")]
    [Display(Name = "Category")]
    public string CategoryId { get; set; }

    public string PhotoURL { get; set; }

    public IFormFile Photo { get; set; }

    [Display(Name = "Under Maintenance")]
    public bool IsMaintenance { get; set; }

    // Selected Amenities Checkboxes (VM Only)
    public List<string> SelectedAmenities { get; set; } = new List<string>();
}

// Member & Order View Models -------------------------------------------------

public class MemberVM
{
    public string Email { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; }

    public string? PhotoURL { get; set; }

    public IFormFile? Photo { get; set; }
}

public class OrderVM
{
    public int OrderId { get; set; }
    public string MemberEmail { get; set; }
    public DateTime OrderDate { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; }

    public decimal TotalAmount { get; set; }
}




