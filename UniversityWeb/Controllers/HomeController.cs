using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace UniversityWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            _logger.LogInformation("Home page accessed, redirecting to Students index");
            return RedirectToAction("Index", "Students");
        }
    }
}