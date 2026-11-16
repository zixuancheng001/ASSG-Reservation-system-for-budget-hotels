using System;
using System.Collections.Generic;

namespace Demo.Models;

public class AvailabilityVM
{
    public int Year { get; set; }

    public int Month { get; set; }

    public List<Product> Products { get; set; } = new();

    public List<ShoppingCartItem> Bookings { get; set; } = new();
}