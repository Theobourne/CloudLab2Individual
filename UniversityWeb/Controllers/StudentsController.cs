using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using Contracts.Models;

namespace UniversityWeb.Controllers
{
    public class StudentsController : Controller
    {
        private readonly HttpClient _studentsHttp;
        private readonly HttpClient _coursesHttp;
        private readonly ILogger<StudentsController> _logger;

        public StudentsController(IHttpClientFactory factory, ILogger<StudentsController> logger)
        {
            _studentsHttp = factory.CreateClient("StudentsApi");
            _coursesHttp = factory.CreateClient("CoursesApi");
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Loading students index page");
            try
            {
                var students = await _studentsHttp.GetFromJsonAsync<List<Student>>("api/Students");
                _logger.LogInformation("Retrieved {StudentCount} students from API", students?.Count ?? 0);
                return View(students);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving students from API");
                return View(new List<Student>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> Enroll(int id)
        {
            _logger.LogInformation("Loading enrollment page for student ID: {StudentId}", id);
            
            try
            {
                var student = await _studentsHttp.GetFromJsonAsync<Student>($"api/Students/{id}");
                if (student == null)
                {
                    _logger.LogWarning("Student with ID: {StudentId} not found", id);
                    return NotFound();
                }

                var courses = await _coursesHttp.GetFromJsonAsync<List<Course>>("api/Courses");
                
                _logger.LogInformation("Retrieved {CourseCount} courses for enrollment selection", courses?.Count ?? 0);
                
                ViewBag.Student = student;
                ViewBag.Courses = courses ?? new List<Course>();
                
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading enrollment page for student ID: {StudentId}", id);
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Enroll(int studentId, int courseId, string title, int credits)
        {
            _logger.LogInformation("Enrolling student {StudentId} in course {CourseId} - {CourseTitle}", 
                studentId, courseId, title);
            
            try
            {
                var enrollmentRequest = new
                {
                    CourseID = courseId,
                    Title = title,
                    Credits = credits
                };

                var response = await _studentsHttp.PostAsJsonAsync($"api/Students/{studentId}/enroll", enrollmentRequest);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully enrolled student {StudentId} in course {CourseId}", 
                        studentId, courseId);
                    TempData["Success"] = "Student enrolled successfully!";
                }
                else
                {
                    _logger.LogWarning("Failed to enroll student {StudentId} in course {CourseId}. Status: {StatusCode}", 
                        studentId, courseId, response.StatusCode);
                    TempData["Error"] = "Failed to enroll student. They may already be enrolled in this course.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enrolling student {StudentId} in course {CourseId}", 
                    studentId, courseId);
                TempData["Error"] = "An error occurred while enrolling the student.";
            }

            return RedirectToAction("Index");
        }
    }
}