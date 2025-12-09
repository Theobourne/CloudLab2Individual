using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CoursesAPI.Data; // Ensure this is your CoursesAPI database context namespace
using CoursesAPI.Models; // Ensure this is your Course model namespace
using Microsoft.Extensions.Logging; // Add this for ILogger
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace CoursesAPI.Controllers
{
    // Sets the base route to 'api/Courses' which Ocelot uses for downstream routing
    [Route("api/[controller]")]
    [ApiController]
    public class CoursesController : ControllerBase
    {
        private readonly CoursesAPIContext _context;
        private readonly ILogger<CoursesController> _logger; // Declare ILogger
        private readonly IDistributedCache _cache;

        public CoursesController(CoursesAPIContext context, ILogger<CoursesController> logger, IDistributedCache cache)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
        }

        // GET: api/Courses
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Course>>> GetCourse()
        {
            _logger.LogInformation("Retrieving all courses");
            
            const string cacheKey = "courses_all";
            
            try
            {
                // Try to get from cache first
                var cachedData = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedData))
                {
                    _logger.LogInformation("Retrieved courses from Redis cache");
                    var courses = JsonSerializer.Deserialize<List<Course>>(cachedData);
                    return courses ?? new List<Course>();
                }

                // If not in cache, get from database
                _logger.LogInformation("Cache miss - retrieving courses from database");
                var coursesFromDb = await _context.Course.ToListAsync();
                
                // Store in cache for 5 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                };
                
                var serializedData = JsonSerializer.Serialize(coursesFromDb);
                await _cache.SetStringAsync(cacheKey, serializedData, cacheOptions);
                
                _logger.LogInformation("Retrieved {CourseCount} courses and cached the result", coursesFromDb.Count);
                return coursesFromDb;
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
            
            var cacheKey = $"course_{id}";
            
            try
            {
                // Try to get from cache first
                var cachedData = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedData))
                {
                    _logger.LogInformation("Retrieved course {CourseId} from Redis cache", id);
                    var course = JsonSerializer.Deserialize<Course>(cachedData);
                    if (course == null)
                    {
                        return NotFound();
                    }
                    return course;
                }

                // If not in cache, get from database
                _logger.LogInformation("Cache miss - retrieving course {CourseId} from database", id);
                var courseFromDb = await _context.Course.FindAsync(id);

                if (courseFromDb == null)
                {
                    _logger.LogWarning("Course with ID: {CourseId} not found", id);
                    return NotFound();
                }

                // Store in cache for 5 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                };
                
                var serializedData = JsonSerializer.Serialize(courseFromDb);
                await _cache.SetStringAsync(cacheKey, serializedData, cacheOptions);

                _logger.LogInformation("Retrieved course: {CourseTitle} (Credits: {Credits}) and cached the result", 
                    courseFromDb.Title, courseFromDb.Credits);
                return courseFromDb;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving course {CourseId}", id);
                throw;
            }
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
                
                // Invalidate cache for this course and all courses list
                await _cache.RemoveAsync($"course_{id}");
                await _cache.RemoveAsync("courses_all");
                _logger.LogInformation("Successfully updated course {CourseId} and invalidated cache", id);
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

                // Invalidate all courses cache
                await _cache.RemoveAsync("courses_all");
                _logger.LogInformation("Successfully created course with ID: {CourseId} and invalidated cache", course.CourseID);
                
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

            // Invalidate cache for this course and all courses list
            await _cache.RemoveAsync($"course_{id}");
            await _cache.RemoveAsync("courses_all");
            _logger.LogInformation("Successfully deleted course {CourseId} and invalidated cache", id);
            
            return NoContent();
        }

        private bool CourseExists(int id)
        {
            // Uses CourseID for existence check
            return _context.Course.Any(e => e.CourseID == id);
        }
    }
}