using CoursesAPI.Models;
using Microsoft.EntityFrameworkCore;
namespace CoursesAPI.Data
{
    public class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new CoursesAPIContext(
            serviceProvider.GetRequiredService<DbContextOptions<CoursesAPIContext>>()))
            {
                context.Database.EnsureCreated();
                // Look for any Student.
                if (context.Course.Any())
                {
                    return; // DB has been seeded
                }
                context.Course.AddRange(
                new Course { CourseID = 1050, Title = "Chemistry", Credits = 3 },
                new Course { CourseID = 4022, Title = "Microeconomics", Credits = 3 },
                new Course { CourseID = 4041, Title = "Macroeconomics", Credits = 3 },
                new Course { CourseID = 1045, Title = "Calculus", Credits = 4 },
                new Course { CourseID = 3141, Title = "Trigonometry", Credits = 4 },
                new Course { CourseID = 2021, Title = "Composition", Credits = 3 },
                new Course { CourseID = 2042, Title = "Literature", Credits = 4 }
                );
                context.SaveChanges();
            }
        }
    }
}
