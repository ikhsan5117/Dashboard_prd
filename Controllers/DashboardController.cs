using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DashboardTest.Models;
using DashboardTest.Repositories;

namespace DashboardTest.Controllers
{
    public class DashboardController : Controller
    {
        private readonly DashboardRepository _repository;

        // Constructor injection of repository
        public DashboardController(DashboardRepository repository)
        {
            _repository = repository;
        }

        public IActionResult Index()
        {
            // Initial view, maybe load empty or default params
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetData(int plantId, DateTime? startDate, DateTime? endDate, string line = null, string jenisNg = null, string kategoriNg = null, string partCode = null)
        {
            if (plantId == 0) plantId = 1; // Default to Plant Hose if not specified
            if (!startDate.HasValue) startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!endDate.HasValue) endDate = DateTime.Now;

            // Ensure endDate includes the entire day (23:59:59)
            endDate = endDate.Value.Date.AddDays(1).AddSeconds(-1);

            try
            {
                var data = await _repository.GetDashboardDataAsync(plantId, startDate.Value, endDate.Value, line, jenisNg, kategoriNg, partCode);
                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetFilterOptions(int plantId)
        {
            if (plantId == 0) plantId = 1;
            try
            {
                var options = await _repository.GetFilterOptionsAsync(plantId);
                return Json(new { success = true, data = options });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
