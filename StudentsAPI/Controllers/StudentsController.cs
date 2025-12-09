using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentsAPI.Data;
using Contracts.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StudentsAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StudentsController : ControllerBase
    {
        private readonly StudentsAPIContext _context;
        private readonly ILogger<StudentsController> _logger;
        private readonly IDistributedCache _cache;
        private readonly JsonSerializerOptions _jsonOptions;

        public StudentsController(StudentsAPIContext context, ILogger<StudentsController> logger, IDistributedCache cache)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
            
            // Configure JSON serialization to handle circular references
            _jsonOptions = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                WriteIndented = false
            };
        }

        // GET: api/Students
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Student>>> GetStudent()
        {
            _logger.LogInformation("Retrieving all students with enrollments");
            
            const string cacheKey = "students_all";
            
            try
            {
                // Try to get from cache first
                var cachedData = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedData))
                {
                    _logger.LogInformation("Retrieved students from Redis cache");
                    var students = JsonSerializer.Deserialize<List<Student>>(cachedData, _jsonOptions);
                    return students ?? new List<Student>();
                }

                // If not in cache, get from database
                _logger.LogInformation("Cache miss - retrieving students from database");
                var studentsFromDb = await _context.Student
                    .Include(s => s.Enrollments)
                    .AsNoTracking()
                    .ToListAsync();
                
                // Store in cache for 5 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                };
                
                var serializedData = JsonSerializer.Serialize(studentsFromDb, _jsonOptions);
                await _cache.SetStringAsync(cacheKey, serializedData, cacheOptions);
                
                _logger.LogInformation("Retrieved {StudentCount} students and cached the result", studentsFromDb.Count);
                return studentsFromDb;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving students");
                throw;
            }
        }

        // GET: api/Students/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Student>> GetStudent(int id)
        {
            _logger.LogInformation("Retrieving student with ID: {StudentId}", id);
            
            var cacheKey = $"student_{id}";
            
            try
            {
                // Try to get from cache first
                var cachedData = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedData))
                {
                    _logger.LogInformation("Retrieved student {StudentId} from Redis cache", id);
                    var student = JsonSerializer.Deserialize<Student>(cachedData, _jsonOptions);
                    if (student == null)
                    {
                        return NotFound();
                    }
                    return student;
                }

                // If not in cache, get from database
                _logger.LogInformation("Cache miss - retrieving student {StudentId} from database", id);
                var studentFromDb = await _context.Student
                    .Include(s => s.Enrollments)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.ID == id);

                if (studentFromDb == null)
                {
                    _logger.LogWarning("Student with ID: {StudentId} not found", id);
                    return NotFound();
                }

                // Store in cache for 5 minutes
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                };
                
                var serializedData = JsonSerializer.Serialize(studentFromDb, _jsonOptions);
                await _cache.SetStringAsync(cacheKey, serializedData, cacheOptions);

                _logger.LogInformation("Retrieved student: {StudentName} with {EnrollmentCount} enrollments and cached the result", 
                    $"{studentFromDb.FirstMidName} {studentFromDb.LastName}", studentFromDb.Enrollments?.Count ?? 0);
                return studentFromDb;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving student {StudentId}", id);
                throw;
            }
        }

        // POST: api/Students/{studentId}/enroll
        [HttpPost("{studentId}/enroll")]
        public async Task<ActionResult<Enrollment>> EnrollStudent(int studentId, [FromBody] EnrollmentRequest request)
        {
            _logger.LogInformation("Enrolling student {StudentId} in course {CourseId} - {CourseTitle}", 
                studentId, request.CourseID, request.Title);
            
            var student = await _context.Student.FindAsync(studentId);
            if (student == null)
            {
                _logger.LogWarning("Enrollment failed: Student {StudentId} not found", studentId);
                return NotFound("Student not found");
            }

            // Check if already enrolled
            var existingEnrollment = await _context.Enrollment
                .FirstOrDefaultAsync(e => e.StudentID == studentId && e.CourseID == request.CourseID);
            
            if (existingEnrollment != null)
            {
                _logger.LogWarning("Student {StudentId} is already enrolled in course {CourseId}", 
                    studentId, request.CourseID);
                return BadRequest("Student is already enrolled in this course");
            }

            var enrollment = new Enrollment
            {
                StudentID = studentId,
                CourseID = request.CourseID,
                Title = request.Title,
                Credits = request.Credits,
                Grade = null
            };

            _context.Enrollment.Add(enrollment);
            await _context.SaveChangesAsync();

            // Invalidate cache for this student and all students list
            await _cache.RemoveAsync($"student_{studentId}");
            await _cache.RemoveAsync("students_all");
            _logger.LogInformation("Cache invalidated for student {StudentId}", studentId);

            _logger.LogInformation("Successfully enrolled student {StudentId} in course {CourseId} - {CourseTitle}", 
                studentId, request.CourseID, request.Title);
            
            return CreatedAtAction(nameof(GetStudent), new { id = studentId }, enrollment);
        }

        // PUT: api/Students/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutStudent(int id, Student student)
        {
            _logger.LogInformation("Updating student with ID: {StudentId}", id);
            
            if (id != student.ID)
            {
                _logger.LogWarning("Update failed: ID mismatch. Route ID: {RouteId}, Student ID: {StudentId}", 
                    id, student.ID);
                return BadRequest();
            }

            _context.Entry(student).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                
                // Invalidate cache for this student and all students list
                await _cache.RemoveAsync($"student_{id}");
                await _cache.RemoveAsync("students_all");
                _logger.LogInformation("Successfully updated student {StudentId} and invalidated cache", id);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!StudentExists(id))
                {
                    _logger.LogWarning("Update failed: Student {StudentId} not found", id);
                    return NotFound();
                }
                else
                {
                    _logger.LogError(ex, "Concurrency error updating student {StudentId}", id);
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Students
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Student>> PostStudent(Student student)
        {
            _logger.LogInformation("Creating new student: {FirstName} {LastName}", 
                student.FirstMidName, student.LastName);
            
            try
            {
                _context.Student.Add(student);
                await _context.SaveChangesAsync();

                // Invalidate all students cache
                await _cache.RemoveAsync("students_all");
                _logger.LogInformation("Successfully created student with ID: {StudentId} and invalidated cache", student.ID);
                
                return CreatedAtAction("GetStudent", new { id = student.ID }, student);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating student: {FirstName} {LastName}", 
                    student.FirstMidName, student.LastName);
                throw;
            }
        }

        // DELETE: api/Students/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            _logger.LogInformation("Deleting student with ID: {StudentId}", id);
            
            var student = await _context.Student.FindAsync(id);
            if (student == null)
            {
                _logger.LogWarning("Delete failed: Student {StudentId} not found", id);
                return NotFound();
            }

            _context.Student.Remove(student);
            await _context.SaveChangesAsync();

            // Invalidate cache for this student and all students list
            await _cache.RemoveAsync($"student_{id}");
            await _cache.RemoveAsync("students_all");
            _logger.LogInformation("Successfully deleted student {StudentId} and invalidated cache", id);
            
            return NoContent();
        }

        private bool StudentExists(int id)
        {
            return _context.Student.Any(e => e.ID == id);
        }
    }

    // DTO for enrollment request
    public class EnrollmentRequest
    {
        public int CourseID { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Credits { get; set; }
    }
}
