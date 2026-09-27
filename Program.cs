using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using WatchesStore.Models;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// Add MVC
// ======================================================
builder.Services.AddControllersWithViews();

// ======================================================
// Database
// ======================================================
builder.Services.AddDbContext<ContextDB>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// ======================================================
// Authentication
// ======================================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";

        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;

        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.Events.OnRedirectToAccessDenied = context =>
        {
            Console.WriteLine(
                $"Access denied for user: {context.HttpContext.User.Identity?.Name}"
            );

            context.Response.Redirect(options.AccessDeniedPath);
            return Task.CompletedTask;
        };
    });

// ======================================================
// Authorization
// ======================================================
builder.Services.AddAuthorization();

// ======================================================
// Rate Limiting - Login
// ======================================================
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("LoginPolicy", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});

// ======================================================
// Build App
// ======================================================
var app = builder.Build();

// ======================================================
// Create / Reset Admin Account
// ======================================================
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ContextDB>();
    var passwordHasher = new PasswordHasher<User>();

    var adminRole = context.Roles
        .FirstOrDefault(r => r.RoleName == "Admin");

    if (adminRole != null)
    {
        var admin = context.Users
            .FirstOrDefault(u => u.Email == "admin@watches.com");

        if (admin == null)
        {
            admin = new User
            {
                FullName = "Admin",
                Email = "admin@watches.com",
                RoleId = adminRole.RoleId,
                FailedLoginAttempts = 0,
                LockoutEnd = null,
                CreatedAt = DateTime.UtcNow
            };

            admin.PasswordHash =
                passwordHasher.HashPassword(admin, "Admin@123");

            context.Users.Add(admin);
        }
        else
        {
            // Reset Admin password
            admin.PasswordHash =
                passwordHasher.HashPassword(admin, "Admin@123");

            admin.FailedLoginAttempts = 0;
            admin.LockoutEnd = null;
            admin.RoleId = adminRole.RoleId;
        }

        context.SaveChanges();
    }
}

// ======================================================
// HTTP Request Pipeline
// ======================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();