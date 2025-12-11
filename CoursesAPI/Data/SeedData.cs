using CoursesAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace CoursesAPI.Data
{
    public class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new CoursesAPIContext(
                serviceProvider.GetRequiredService<DbContextOptions<CoursesAPIContext>>()))
            {
                var logger = serviceProvider.GetRequiredService<ILogger<CoursesAPIContext>>();
                
                try
                {
                    // First, ensure database exists
                    context.Database.EnsureCreated();
                    
                    // Check if Course table exists
                    bool courseTableExists = CheckIfCourseTableExists(context, logger);
                    
                    if (!courseTableExists)
                    {
                        logger.LogWarning("Course table does not exist. Creating table...");
                        CreateCourseTable(context, logger);
                    }
                    
                    // Check if we need to seed data
                    if (context.Course.Any())
                    {
                        logger.LogInformation("Course table already has data. Skipping seed.");
                        return; // DB has been seeded
                    }
                    
                    // Seed course data
                    logger.LogInformation("Seeding Course table with initial data...");
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
                    logger.LogInformation("Course table seeded successfully with {Count} courses.", 7);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while seeding the Course table.");
                    throw;
                }
            }
        }
        
        private static bool CheckIfCourseTableExists(CoursesAPIContext context, ILogger logger)
        {
            try
            {
                // Try to query the Course table
                _ = context.Course.Any();
                logger.LogInformation("Course table exists.");
                return true;
            }
            catch (SqlException ex) when (ex.Number == 208) // Invalid object name
            {
                logger.LogWarning("Course table does not exist (SQL Error 208).");
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking if Course table exists.");
                throw;
            }
        }
        
        private static void CreateCourseTable(CoursesAPIContext context, ILogger logger)
        {
            try
            {
                // Create the Course table using raw SQL
                var createTableSql = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Course')
                    BEGIN
                        CREATE TABLE Course (
                            CourseID INT PRIMARY KEY NOT NULL,
                            Title NVARCHAR(50) NOT NULL,
                            Credits INT NOT NULL
                        );
                    END";
                
                context.Database.ExecuteSqlRaw(createTableSql);
                logger.LogInformation("Course table created successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create Course table.");
                throw;
            }
        }
    }
}
