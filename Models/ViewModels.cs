using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Demo.Models;

#nullable disable warnings

public class DB : DbContext
{
    public DB(DbContextOptions options) : base(options) { }

    // DB Sets
    public DbSet<User> Users { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<Member> Members { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ShoppingCart> ShoppingCarts { get; set; }
    public DbSet<ShoppingCartItem> ShoppingCartItems { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<WishlistItem> WishlistItems { get; set; }
    public DbSet<RoomReview> RoomReviews { get; set; }
    public DbSet<Coupon> Coupons { get; set; }
}

// Entity Classes -------------------------------------------------------------

public class User
{
    [Key, MaxLength(100)]
    public string Email { get; set; }
    [MaxLength(100)]
    public string Hash { get; set; }
    [MaxLength(100)]
    public string Name { get; set; }
    [MaxLength(100)]
    public string PhotoURL { get; set; }
    public bool IsTemporaryPassword { get; set; } = false;

    [MaxLength(10)]
    public string Theme { get; set; } = "Light";
    public bool IsBlocked { get; set; } = false;
    public string Role => GetType().Name;
}

public class Admin : User { }
public class Member : User { }

public class Category
{
    [Key, MaxLength(10)]
    public string CategoryId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; }

    [MaxLength(255)]
    public string Description { get; set; }

    // Featured Status Property
    public bool IsFeatured { get; set; } = false;

    // Navigation Property
    public List<Product> Products { get; set; } = new();
}

public class Product
{
    [Key, MaxLength(10)]
    public string ProductId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } // e.g. Room Number / Room Name

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; } // Price per night

    public int Capacity { get; set; }

    public bool IsAvailable { get; set; } = true;

    // ADD THIS LINE
    public bool IsMaintenance { get; set; } = false;

    // ADD THIS LINE (if missing)
    public string Amenities { get; set; } = "";

    [MaxLength(255)]
    public string PhotoURL { get; set; }

    // Foreign Key mapping to Category
    [Required, MaxLength(10)]
    public string CategoryId { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public Category Category { get; set; }
}
public class ShoppingCart
{
    [Key]
    public int CartId { get; set; }

    [Required, MaxLength(100)]
    public string MemberEmail { get; set; }

    [ForeignKey(nameof(MemberEmail))]
    public Member Member { get; set; }

    public List<ShoppingCartItem> Items { get; set; } = new();
}
public class ShoppingCartItem
{
    [Key]
    public int CartItemId { get; set; }

    [Required]
    public int CartId { get; set; }

    [ForeignKey(nameof(CartId))]
    public ShoppingCart Cart { get; set; }

    [Required, MaxLength(10)]
    public string ProductId { get; set; }

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; }

    [Range(1, 10)]
    public int Quantity { get; set; } = 1;

    [Required]
    public DateTime CheckInDate { get; set; }

    [Required]
    public DateTime CheckOutDate { get; set; }
}

public class Order
{
    [Key]
    public int OrderId { get; set; }

    [Required, MaxLength(100)]
    public string MemberEmail { get; set; }

    [ForeignKey(nameof(MemberEmail))]
    public Member Member { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.Now;

    [Required, MaxLength(20)]
    public string Status { get; set; } = "Pending";

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [MaxLength(30)]
    public string? CouponCode { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    [Key]
    public int OrderItemId { get; set; }

    [Required]
    public int OrderId { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order Order { get; set; }

    [Required, MaxLength(10)]
    public string ProductId { get; set; }

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; }

    [Range(1, 10)]
    public int Quantity { get; set; } = 1;

    [Required]
    public DateTime CheckInDate { get; set; }

    [Required]
    public DateTime CheckOutDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }
}

public class WishlistItem
{
    [Key] public int WishlistItemId { get; set; }
    [Required, MaxLength(100)] public string MemberEmail { get; set; } = "";
    [Required, MaxLength(10)] public string ProductId { get; set; } = "";
    public DateTime AddedAt { get; set; } = DateTime.Now;
    [ForeignKey(nameof(MemberEmail))] public Member? Member { get; set; }
    [ForeignKey(nameof(ProductId))] public Product? Product { get; set; }
}

public class RoomReview
{
    [Key] public int ReviewId { get; set; }
    [Required, MaxLength(10)] public string ProductId { get; set; } = "";
    [Required, MaxLength(100)] public string MemberEmail { get; set; } = "";
    [Range(1, 5)] public int Rating { get; set; }
    [Required, MaxLength(500)] public string Comment { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    [ForeignKey(nameof(ProductId))] public Product? Product { get; set; }
    [ForeignKey(nameof(MemberEmail))] public Member? Member { get; set; }
}

public class Coupon
{
    [Key, MaxLength(30)] public string Code { get; set; } = "";
    [Range(0.01, 100)]
    [Column(TypeName = "decimal(5,2)")]
    public decimal DiscountPercent { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
}

    public class EditAdminVM
    {
        public string Email { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        public string Name { get; set; }

        public string? PhotoURL { get; set; }
    }
