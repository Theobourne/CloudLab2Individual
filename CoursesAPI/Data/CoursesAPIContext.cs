using CoursesAPI.Models; // ⬅️ Change 1: Use the correct Models namespace for this project
using Microsoft.EntityFrameworkCore;

namespace CoursesAPI.Data // ⬅️ Change 2: Use the correct Data namespace for this project
{
    public class CoursesAPIContext : DbContext
    {
        public CoursesAPIContext(DbContextOptions<CoursesAPIContext> options)
            : base(options)
        {
        }

        // ⬅️ Change 3: Only include the DbSet for the Course model
        public DbSet<Course> Course { get; set; } = default!;
    }
}