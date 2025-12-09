using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using Contracts.Models;

namespace UniversityWeb.Controllers
{
    public class CoursesController : Controller
    {
        private readonly HttpClient _http;
        private readonly ILogger<CoursesController> _logger;
        
        public CoursesController(IHttpClientFactory factory, ILogger<CoursesController> logger)
        {
            _http = factory.CreateClient("CoursesApi");
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Loading courses index page");
            try
            {
                var courses = await _http.GetFromJsonAsync<List<Course>>("api/Courses");
                _logger.LogInformation("Retrieved {CourseCount} courses from API", courses?.Count ?? 0);
                return View(courses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving courses from API");
                return View(new List<Course>());
            }
        }
    }
}