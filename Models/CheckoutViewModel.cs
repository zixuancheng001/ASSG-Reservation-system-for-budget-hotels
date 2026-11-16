#nullable disable
using System.ComponentModel.DataAnnotations;


namespace Demo.Models;

public class CheckoutViewModel
{
    // --- Renter detail ---
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; }

    [Required(ErrorMessage = "Email Address is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    [Display(Name = "Email Address")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Phone Number is required.")]
    [RegularExpression(@"^01[0-46-9]-\d{7,8}$", ErrorMessage = "Please enter a valid Malaysian phone number.")]
    [Display(Name = "Phone Number")]
    public string Phone { get; set; }

    // --- Payment Details ---
    [Required(ErrorMessage = "Please select a payment method.")]
    [Display(Name = "Payment Method")]
    public string PaymentMethod { get; set; }

    [Display(Name = "Cardholder Name")]
    public string CardName { get; set; }

    //[CreditCard(ErrorMessage = "Invalid Credit Card Number.")]
    [Display(Name = "Card Number")]
    public string CardNumber { get; set; }

    [RegularExpression(@"^(0[1-9]|1[0-2])\/\d{2}$", ErrorMessage = "Expiry Date must be MM/YY format.")]
    [Display(Name = "Expiry Date (MM/YY)")]
    public string ExpiryDate { get; set; }

    [RegularExpression(@"^\d{3,4}$", ErrorMessage = "CVV must be 3 or 4 digits.")]
    [Display(Name = "CVV")]
    public string CVV { get; set; }

    public string? BankSelect { get; set; }

    [StringLength(30)]
    [Display(Name = "Promo Code")]
    public string? CouponCode { get; set; }
}
