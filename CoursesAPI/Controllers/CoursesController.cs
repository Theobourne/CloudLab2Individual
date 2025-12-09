using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CoursesAPI.Data; // Ensure this is your CoursesAPI database context namespace
using CoursesAPI.Models; // Ensure this is your Course model namespace
using Microsoft.Extensions.Logging; // Add this for ILogger

namespace CoursesAPI.Controllers
{
    // Sets the base route to 'api/Courses' which Ocelot uses for downstream routing
    [Route("api/[controller]")]
    [ApiController]
    public class CoursesController : ControllerBase
    {
        private readonly CoursesAPIContext _context;
        private readonly ILogger<CoursesController> _logger; // Declare ILogger

        public CoursesController(CoursesAPIContext context, ILogger<CoursesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Courses
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Course>>> GetCourse()
        {
            _logger.LogInformation("Retrieving all courses");
            try
            {
                // Fetch all courses from the database
                // Assuming your DbSet in CoursesAPIContext is named 'Course'
                var courses = await _context.Course.ToListAsync();
                _logger.LogInformation("Retrieved {CourseCount} courses", courses.Count);
                return courses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving courses");
                throw;
            }
        }

        // GET: api/Courses/5
        // Uses CourseID for lookup
        [HttpGet("{id}")]
        public async Task<ActionResult<Course>> GetCourse(int id)
        {
            _logger.LogInformation("Retrieving course with ID: {CourseId}", id);
            
            // FindAsync uses the primary key, which is CourseID
            var course = await _context.Course.FindAsync(id);

            if (course == null)
            {
                _logger.LogWarning("Course with ID: {CourseId} not found", id);
                return NotFound();
            }

            _logger.LogInformation("Retrieved course: {CourseTitle} (Credits: {Credits})", 
                course.Title, course.Credits);
            return course;
        }

        // PUT: api/Courses/5
        // Uses CourseID for comparison
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCourse(int id, Course course)
        {
            _logger.LogInformation("Updating course with ID: {CourseId}", id);
            
            if (id != course.CourseID) // Check against CourseID
            {
                _logger.LogWarning("Update failed: ID mismatch. Route ID: {RouteId}, Course ID: {CourseId}", 
                    id, course.CourseID);
                return BadRequest();
            }

            _context.Entry(course).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully updated course {CourseId}", id);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!CourseExists(id))
                {
                    _logger.LogWarning("Update failed: Course {CourseId} not found", id);
                    return NotFound();
                }
                else
                {
                    _logger.LogError(ex, "Concurrency error updating course {CourseId}", id);
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Courses
        [HttpPost]
        public async Task<ActionResult<Course>> PostCourse(Course course)
        {
            _logger.LogInformation("Creating new course: {CourseTitle} (Credits: {Credits})", 
                course.Title, course.Credits);
            
            try
            {
                _context.Course.Add(course);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully created course with ID: {CourseId}", course.CourseID);
                // Use CourseID when creating the resource URL
                return CreatedAtAction("GetCourse", new { id = course.CourseID }, course);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating course: {CourseTitle}", course.Title);
                throw;
            }
        }

        // DELETE: api/Courses/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            _logger.LogInformation("Deleting course with ID: {CourseId}", id);
            
            var course = await _context.Course.FindAsync(id);
            if (course == null)
            {
                _logger.LogWarning("Delete failed: Course {CourseId} not found", id);
                return NotFound();
            }

            _context.Course.Remove(course);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted course {CourseId}", id);
            return NoContent();
        }

        private bool CourseExists(int id)
        {
            // Uses CourseID for existence check
            return _context.Course.Any(e => e.CourseID == id);
        }
    }
}