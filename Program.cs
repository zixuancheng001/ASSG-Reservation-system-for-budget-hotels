global using Demo.Models;
global using Demo;
global using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Enable Session state management
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(15);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddDbContext<DB>(options =>
{
    options.UseSqlServer($@"
        Data Source=(LocalDB)\MSSQLLocalDB;
        AttachDbFilename={builder.Environment.ContentRootPath}\DB.mdf;
    ");
    // Suppress the pending model changes warning.
    // Schema changes are handled via raw SQL in EnsureXxx methods.
    options.ConfigureWarnings(w =>
        w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});
builder.Services.AddScoped<Helper>();
builder.Services.AddAuthentication("Cookies").AddCookie("Cookies", options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DB>();
    db.Database.Migrate();
    EnsureOrderTables(db);
    EnsureAdditionalFeatureTables(db);
    NormalizePaidOrders(db);
    UpdateCategoryDescriptions(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

// Enable Session Middleware (must be after UseRouting and before UseAuthorization)
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();
app.UseRequestLocalization("en-MY");
app.MapDefaultControllerRoute();
app.Run();

static void EnsureOrderTables(DB db)
{
    db.Database.ExecuteSqlRaw("""
        IF OBJECT_ID(N'[dbo].[Orders]', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[Orders] (
                [OrderId]     int IDENTITY(1,1) NOT NULL,
                [MemberEmail] nvarchar(100)     NOT NULL,
                [OrderDate]   datetime2         NOT NULL,
                [Status]      nvarchar(20)      NOT NULL,
                [TotalAmount] decimal(18,2)     NOT NULL,
                CONSTRAINT [PK_Orders] PRIMARY KEY ([OrderId]),
                CONSTRAINT [FK_Orders_Users_MemberEmail]
                    FOREIGN KEY ([MemberEmail]) REFERENCES [dbo].[Users] ([Email]) ON DELETE CASCADE
            );
        END
        """);

    db.Database.ExecuteSqlRaw("""
        IF OBJECT_ID(N'[dbo].[OrderItems]', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[OrderItems] (
                [OrderItemId]  int IDENTITY(1,1) NOT NULL,
                [OrderId]      int               NOT NULL,
                [ProductId]    nvarchar(10)      NOT NULL,
                [Quantity]     int               NOT NULL,
                [CheckInDate]  datetime2         NOT NULL,
                [CheckOutDate] datetime2         NOT NULL,
                [Price]        decimal(18,2)     NOT NULL,
                CONSTRAINT [PK_OrderItems] PRIMARY KEY ([OrderItemId]),
                CONSTRAINT [FK_OrderItems_Orders_OrderId]
                    FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders] ([OrderId]) ON DELETE CASCADE,
                CONSTRAINT [FK_OrderItems_Products_ProductId]
                    FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([ProductId]) ON DELETE CASCADE
            );
        END
        """);

    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT 1 FROM sys.indexes
            WHERE name = N'IX_Orders_MemberEmail'
            AND object_id = OBJECT_ID(N'[dbo].[Orders]'))
        CREATE INDEX [IX_Orders_MemberEmail] ON [dbo].[Orders] ([MemberEmail]);
        """);

    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT 1 FROM sys.indexes
            WHERE name = N'IX_OrderItems_OrderId'
            AND object_id = OBJECT_ID(N'[dbo].[OrderItems]'))
        CREATE INDEX [IX_OrderItems_OrderId] ON [dbo].[OrderItems] ([OrderId]);
        """);

    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT 1 FROM sys.indexes
            WHERE name = N'IX_OrderItems_ProductId'
            AND object_id = OBJECT_ID(N'[dbo].[OrderItems]'))
        CREATE INDEX [IX_OrderItems_ProductId] ON [dbo].[OrderItems] ([ProductId]);
        """);
}

static void UpdateCategoryDescriptions(DB db)
{
    db.Database.ExecuteSqlRaw("UPDATE Categories SET Description = 'Basic rooms for essential stays'    WHERE Name = 'Standard Room'");
    db.Database.ExecuteSqlRaw("UPDATE Categories SET Description = 'Upgraded rooms with better views'  WHERE Name = 'Deluxe Room'");
    db.Database.ExecuteSqlRaw("UPDATE Categories SET Description = 'Spacious suites for business stays' WHERE Name = 'Executive Suite'");
    db.Database.ExecuteSqlRaw("UPDATE Categories SET Description = 'Rooms designed for families'       WHERE Name = 'Family Suite'");
    db.Database.ExecuteSqlRaw("UPDATE Categories SET Description = 'Top-tier luxury suites'            WHERE Name = 'Presidential Suite'");
}

static void NormalizePaidOrders(DB db)
{
    db.Database.ExecuteSqlRaw("""
        IF OBJECT_ID(N'[dbo].[Orders]', N'U') IS NOT NULL
        BEGIN
            UPDATE [dbo].[Orders]
            SET [Status] = N'Completed'
            WHERE [Status] = N'Paid';
        END
        """);
}

static void EnsureAdditionalFeatureTables(DB db)
{
    db.Database.ExecuteSqlRaw("""
        IF OBJECT_ID(N'[dbo].[WishlistItems]', N'U') IS NULL
        CREATE TABLE [dbo].[WishlistItems] (
            [WishlistItemId] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
            [MemberEmail] nvarchar(100) NOT NULL,
            [ProductId] nvarchar(10) NOT NULL,
            [AddedAt] datetime2 NOT NULL,
            CONSTRAINT [UQ_WishlistItems_Member_Product] UNIQUE ([MemberEmail], [ProductId]),
            CONSTRAINT [FK_WishlistItems_Users] FOREIGN KEY ([MemberEmail]) REFERENCES [dbo].[Users]([Email]) ON DELETE CASCADE,
            CONSTRAINT [FK_WishlistItems_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([ProductId]) ON DELETE CASCADE
        );
        """);

    db.Database.ExecuteSqlRaw("""
        IF OBJECT_ID(N'[dbo].[RoomReviews]', N'U') IS NULL
        CREATE TABLE [dbo].[RoomReviews] (
            [ReviewId] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
            [ProductId] nvarchar(10) NOT NULL,
            [MemberEmail] nvarchar(100) NOT NULL,
            [Rating] int NOT NULL CHECK ([Rating] BETWEEN 1 AND 5),
            [Comment] nvarchar(500) NOT NULL,
            [CreatedAt] datetime2 NOT NULL,
            CONSTRAINT [UQ_RoomReviews_Member_Product] UNIQUE ([MemberEmail], [ProductId]),
            CONSTRAINT [FK_RoomReviews_Users] FOREIGN KEY ([MemberEmail]) REFERENCES [dbo].[Users]([Email]) ON DELETE CASCADE,
            CONSTRAINT [FK_RoomReviews_Products] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([ProductId]) ON DELETE CASCADE
        );
        """);

    db.Database.ExecuteSqlRaw("""
        IF OBJECT_ID(N'[dbo].[Coupons]', N'U') IS NULL
        CREATE TABLE [dbo].[Coupons] (
            [Code] nvarchar(30) NOT NULL PRIMARY KEY,
            [DiscountPercent] decimal(5,2) NOT NULL CHECK ([DiscountPercent] > 0 AND [DiscountPercent] <= 100),
            [ExpiryDate] datetime2 NULL,
            [IsActive] bit NOT NULL
        );
        IF NOT EXISTS (SELECT 1 FROM [dbo].[Coupons] WHERE [Code] = N'WELCOME10')
            INSERT INTO [dbo].[Coupons] ([Code], [DiscountPercent], [ExpiryDate], [IsActive]) VALUES (N'WELCOME10', 10, NULL, 1);
        IF NOT EXISTS (SELECT 1 FROM [dbo].[Coupons] WHERE [Code] = N'STAY15')
            INSERT INTO [dbo].[Coupons] ([Code], [DiscountPercent], [ExpiryDate], [IsActive]) VALUES (N'STAY15', 15, NULL, 1);
        """);

    db.Database.ExecuteSqlRaw("""
        IF COL_LENGTH(N'[dbo].[Orders]', N'CouponCode') IS NULL
            ALTER TABLE [dbo].[Orders] ADD [CouponCode] nvarchar(30) NULL;
        IF COL_LENGTH(N'[dbo].[Orders]', N'DiscountAmount') IS NULL
            ALTER TABLE [dbo].[Orders] ADD [DiscountAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_Orders_DiscountAmount] DEFAULT 0;
        """);

    // Add IsBlocked column if it was added after the initial migration
    db.Database.ExecuteSqlRaw("""
        IF COL_LENGTH(N'[dbo].[Users]', N'IsBlocked') IS NULL
            ALTER TABLE [dbo].[Users] ADD [IsBlocked] bit NOT NULL CONSTRAINT [DF_Users_IsBlocked] DEFAULT 0;
        """);
}
