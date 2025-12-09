using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Text.Json;

namespace UniversityWeb.Controllers
{
    public class HealthController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public HealthController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> Index()
        {
            var healthChecks = new List<HealthCheckResult>();

            // Check Students API
            healthChecks.Add(await CheckService("StudentsAPI", "http://studentsapi:8080/health"));

            // Check Courses API
            healthChecks.Add(await CheckService("CoursesAPI", "http://coursesapi:8080/health"));

            return View(healthChecks);
        }

        private async Task<HealthCheckResult> CheckService(string name, string url)
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            try
            {
                var response = await client.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var healthReport = JsonSerializer.Deserialize<HealthReport>(content, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });

                    return new HealthCheckResult
                    {
                        Name = name,
                        Status = healthReport?.Status ?? "Unknown",
                        LastExecution = DateTime.Now,
                        Duration = healthReport?.TotalDuration ?? "0ms",
                        Checks = healthReport?.Entries?.Select(e => new HealthCheckEntry
                        {
                            Name = e.Key,
                            Status = e.Value.Status,
                            Description = e.Value.Description,
                            Duration = e.Value.Duration
                        }).ToList() ?? new List<HealthCheckEntry>()
                    };
                }
                else
                {
                    return new HealthCheckResult
                    {
                        Name = name,
                        Status = "Unhealthy",
                        LastExecution = DateTime.Now,
                        Duration = "0ms",
                        Checks = new List<HealthCheckEntry>()
                    };
                }
            }
            catch (Exception ex)
            {
                return new HealthCheckResult
                {
                    Name = name,
                    Status = "Unhealthy",
                    LastExecution = DateTime.Now,
                    Duration = "0ms",
                    Error = ex.Message,
                    Checks = new List<HealthCheckEntry>()
                };
            }
        }
    }

    public class HealthCheckResult
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime LastExecution { get; set; }
        public string Duration { get; set; } = string.Empty;
        public string? Error { get; set; }
        public List<HealthCheckEntry> Checks { get; set; } = new();
    }

    public class HealthCheckEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Duration { get; set; } = string.Empty;
    }

    public class HealthReport
    {
        public string Status { get; set; } = string.Empty;
        public string TotalDuration { get; set; } = string.Empty;
        public Dictionary<string, HealthEntry> Entries { get; set; } = new();
    }

    public class HealthEntry
    {
        public string Status { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
    }
}
