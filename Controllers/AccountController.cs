using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using WatchesStore.Models;
using WatchesStore.Models.ViewModels;

namespace WatchesStore.Controllers
{
    // Person 2 - Authentication & Authorization
    // Handles Register / Login / Logout, role assignment (User/Admin)
    // and the logged-in user's profile page.
    public class AccountController : Controller
    {
        private const int MaxFailedAttempts = 5;
        private const int LockoutMinutes = 15;

        private readonly ContextDB _context;
        private readonly PasswordHasher<User> _passwordHasher = new();
        private readonly ILogger<AccountController> _logger;

        public AccountController(ContextDB context, ILogger<AccountController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var emailExists = await _context.Users
                .AnyAsync(u => u.Email == model.Email);

            if (emailExists)
            {
                ModelState.AddModelError(nameof(model.Email), "This email is already registered.");
                return View(model);
            }

            // Every new sign-up gets the "User" role.
            // Admin accounts are not created through this form.
            var userRole = await _context.Roles
                .FirstOrDefaultAsync(r => r.RoleName == "User");

            if (userRole == null)
            {
                ModelState.AddModelError(string.Empty, "Registration is unavailable right now. Please try again later.");
                return View(model);
            }

            var user = new User
            {
                FullName = model.FullName.Trim(),
                Email = model.Email.Trim(),
                RoleId = userRole.RoleId
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New user registered: {Email}", user.Email);

            await SignInUserAsync(user, userRole.RoleName, rememberMe: false);

            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("LoginPolicy")]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == model.Email);

            if (user == null)
            {
                _logger.LogWarning("Login failed: no account found for {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            // Account temporarily locked from repeated failed attempts.
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            {
                var minutesLeft = (int)Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes);

                _logger.LogWarning("Login blocked, account locked: {Email}", model.Email);
                ModelState.AddModelError(string.Empty,
                    $"This account is temporarily locked due to multiple failed sign-in attempts. Try again in {minutesLeft} minute(s).");
                return View(model);
            }

            var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);

            if (verification == PasswordVerificationResult.Failed)
            {
                user.FailedLoginAttempts++;

                if (user.FailedLoginAttempts >= MaxFailedAttempts)
                {
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                    user.FailedLoginAttempts = 0;

                    _logger.LogWarning(
                        "Account locked for {LockoutMinutes} minutes after {MaxAttempts} failed attempts: {Email}",
                        LockoutMinutes, MaxFailedAttempts, model.Email);
                }
                else
                {
                    _logger.LogWarning(
                        "Login failed: wrong password for {Email} (attempt {Attempt}/{Max})",
                        model.Email, user.FailedLoginAttempts, MaxFailedAttempts);
                }

                await _context.SaveChangesAsync();

                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            // Successful login: reset any lockout counters.
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {Email} logged in successfully", model.Email);

            await SignInUserAsync(user, user.Role.RoleName, model.RememberMe);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // GET: /Account/Profile
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction(nameof(Login));
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            return View(user);
        }

        private async Task SignInUserAsync(User user, string roleName, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, roleName)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(rememberMe ? 30 : 1)
                });
        }
    }
}
