using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using WatchesStore.Models;

namespace WatchesStore.Controllers
{
    public class HomeController : Controller
    {
        private readonly ContextDB _context;

        public HomeController(ContextDB context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var watches = await _context.Watches
                .Include(w => w.Brand)
                .Where(w => w.IsAvailable)
                .ToListAsync();

            return View(watches);
        }

        public async Task<IActionResult> Details(int id)
        {
            var watch = await _context.Watches
                .Include(w => w.Brand)
                .FirstOrDefaultAsync(w => w.WatchId == id);

            if (watch == null)
            {
                return NotFound();
            }

            return View(watch);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id
                    ?? HttpContext.TraceIdentifier
            });
        }
    }
}