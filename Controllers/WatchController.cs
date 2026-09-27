using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WatchesStore.Models;

namespace WatchesStore.Controllers
{
    [Authorize(Roles = "Admin")]
    public class WatchController : Controller
    {
        private readonly ContextDB _context;

        public WatchController(ContextDB context)
        {
            _context = context;
        }

        // =====================================================
        // Admin Dashboard
        // =====================================================
        public IActionResult Index()
        {
            var watches = _context.Watches
                .Include(w => w.Brand)
                .OrderByDescending(w => w.CreatedAt)
                .ToList();

            return View(watches);
        }

        // =====================================================
        // Details
        // =====================================================
        public IActionResult Details(int id)
        {
            var watch = _context.Watches
                .Include(w => w.Brand)
                .FirstOrDefault(w => w.WatchId == id);

            if (watch == null)
                return NotFound();

            return View(watch);
        }

        // =====================================================
        // Create - GET
        // =====================================================
        public IActionResult Create()
        {
            LoadBrands();

            return View();
        }

        // =====================================================
        // Create - POST
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Watch watch)
        {
            if (ModelState.IsValid)
            {
                _context.Watches.Add(watch);
                _context.SaveChanges();

                return RedirectToAction(nameof(Index));
            }

            LoadBrands(watch.BrandId);

            return View(watch);
        }

        // =====================================================
        // Edit - GET
        // =====================================================
        public IActionResult Edit(int id)
        {
            var watch = _context.Watches
                .FirstOrDefault(w => w.WatchId == id);

            if (watch == null)
                return NotFound();

            LoadBrands(watch.BrandId);

            return View(watch);
        }

        // =====================================================
        // Edit - POST
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, Watch watch)
        {
            if (id != watch.WatchId)
                return NotFound();

            if (ModelState.IsValid)
            {
                _context.Watches.Update(watch);
                _context.SaveChanges();

                return RedirectToAction(nameof(Index));
            }

            LoadBrands(watch.BrandId);

            return View(watch);
        }

        // =====================================================
        // Change Availability
        // =====================================================
        public IActionResult ChangeAvailability(int id)
        {
            var watch = _context.Watches
                .FirstOrDefault(w => w.WatchId == id);

            if (watch == null)
                return NotFound();

            watch.IsAvailable = !watch.IsAvailable;

            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }

        //======================================================
        //                   Delete
        //======================================================

        public IActionResult Delete(int id ,Watch watch)
        {
             watch = _context.Watches.FirstOrDefault(w => w.WatchId == id);
            if(watch == null)
            {
                return NotFound();
            }
            else
            {
                _context.Watches.Remove(watch);
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        // =====================================================
        // Load Brands for Create / Edit
        // =====================================================
        private void LoadBrands(int? selectedBrandId = null)
        {
            ViewBag.Brands = new SelectList(
                _context.Brands
                    .OrderBy(b => b.BrandName)
                    .ToList(),
                "BrandId",
                "BrandName",
                selectedBrandId
            );
        }
    }
}
