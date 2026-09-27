using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WatchesStore.Models;

namespace WatchesStore.Controllers
{
    // Admin Dashboard - restricted to Admin role only
    [Authorize(Roles = "Admin")]
    public class BrandController : Controller
    {
        private readonly ContextDB _context;

        public BrandController(ContextDB context)
        {
            _context = context;
        }

        // View All Brands
        public IActionResult Index()
        {
            var brands = _context.Brands.ToList();

            return View(brands);
        }

        // Add Brand
        public IActionResult Create()
        {
            return View();
        }

        // Save Brand
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Brand brand)
        {
            if (ModelState.IsValid)
            {
                _context.Brands.Add(brand);
                _context.SaveChanges();

                return RedirectToAction(nameof(Index));
            }

            return View(brand);
        }

        // Edit Brand
        public IActionResult Edit(int id)
        {
            var brand = _context.Brands
                .FirstOrDefault(b => b.BrandId == id);

            if (brand == null)
            {
                return NotFound();
            }

            return View(brand);
        }

        // Save Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Brand brand)
        {
            if (id != brand.BrandId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                _context.Brands.Update(brand);
                _context.SaveChanges();

                return RedirectToAction(nameof(Index));
            }

            return View(brand);
        }
    }
}