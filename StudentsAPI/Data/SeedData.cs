using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Contracts.Models;

namespace StudentsAPI.Data
{
    public class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new StudentsAPIContext(
                serviceProvider.GetRequiredService<DbContextOptions<StudentsAPIContext>>()))
            {
                var logger = serviceProvider.GetRequiredService<ILogger<StudentsAPIContext>>();
                
                try
                {
                    // Ensure database exists
                    context.Database.EnsureCreated();
                    
                    // Check if Student table exists
                    bool studentTableExists = CheckIfTableExists(context, "Student", logger);
                    if (!studentTableExists)
                    {
                        logger.LogWarning("Student table does not exist. Creating table...");
                        CreateStudentTable(context, logger);
                    }
                    
                    // Check if Enrollment table exists
                    bool enrollmentTableExists = CheckIfTableExists(context, "Enrollment", logger);
                    if (!enrollmentTableExists)
                    {
                        logger.LogWarning("Enrollment table does not exist. Creating table...");
                        CreateEnrollmentTable(context, logger);
                    }
                    
                    // Check if we need to seed student data
                    if (context.Student.Any())
                    {
                        logger.LogInformation("Student table already has data. Skipping seed.");
                        return;   // DB has been seeded
                    }
                    
                    // Seed student data
                    logger.LogInformation("Seeding Student and Enrollment tables with initial data...");
                    context.Student.AddRange(
                        new Student { FirstMidName = "Carson", LastName = "Alexander", EnrollmentDate = DateTime.Parse("2005-09-01") },
                        new Student { FirstMidName = "Meredith", LastName = "Alonso", EnrollmentDate = DateTime.Parse("2002-09-01") },
                        new Student { FirstMidName = "Arturo", LastName = "Anand", EnrollmentDate = DateTime.Parse("2003-09-01") },
                        new Student { FirstMidName = "Gytis", LastName = "Barzdukas", EnrollmentDate = DateTime.Parse("2002-09-01") },
                        new Student { FirstMidName = "Yan", LastName = "Li", EnrollmentDate = DateTime.Parse("2002-09-01") },
                        new Student { FirstMidName = "Peggy", LastName = "Justice", EnrollmentDate = DateTime.Parse("2001-09-01") },
                        new Student { FirstMidName = "Laura", LastName = "Norman", EnrollmentDate = DateTime.Parse("2003-09-01") },
                        new Student { FirstMidName = "Nino", LastName = "Olivetto", EnrollmentDate = DateTime.Parse("2005-09-01") }
                    );
                    context.SaveChanges();
                    
                    context.Enrollment.AddRange(
                        new Enrollment { StudentID = 1, CourseID = 1050, Title = "Chemistry", Credits = 3, Grade = Grade.A },
                        new Enrollment { StudentID = 1, CourseID = 4022, Title = "Microeconomics", Credits = 3, Grade = Grade.C },
                        new Enrollment { StudentID = 1, CourseID = 4041, Title = "Macroeconomics", Credits = 3, Grade = Grade.B },
                        new Enrollment { StudentID = 2, CourseID = 1045, Title = "Calculus", Credits = 4, Grade = Grade.B },
                        new Enrollment { StudentID = 2, CourseID = 3141, Title = "Trigonometry", Credits = 4, Grade = Grade.F },
                        new Enrollment { StudentID = 2, CourseID = 2021, Title = "Composition", Credits = 3, Grade = Grade.F },
                        new Enrollment { StudentID = 3, CourseID = 1050, Title = "Chemistry", Credits = 3 },
                        new Enrollment { StudentID = 4, CourseID = 1050, Title = "Chemistry", Credits = 3 },
                        new Enrollment { StudentID = 4, CourseID = 4022, Title = "Microeconomics", Credits = 3, Grade = Grade.F },
                        new Enrollment { StudentID = 5, CourseID = 4041, Title = "Macroeconomics", Credits = 3, Grade = Grade.C },
                        new Enrollment { StudentID = 6, CourseID = 1045, Title = "Calculus", Credits = 4 },
                        new Enrollment { StudentID = 7, CourseID = 3141, Title = "Trigonometry", Credits = 4, Grade = Grade.A }
                    );
                    context.SaveChanges();
                    logger.LogInformation("Student and Enrollment tables seeded successfully.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while seeding the Student and Enrollment tables.");
                    throw;
                }
            }
        }
        
        private static bool CheckIfTableExists(StudentsAPIContext context, string tableName, ILogger logger)
        {
            try
            {
                var sql = $"SELECT CASE WHEN EXISTS (SELECT * FROM sys.tables WHERE name = '{tableName}') THEN 1 ELSE 0 END";
                var exists = context.Database.SqlQueryRaw<int>(sql).ToList().FirstOrDefault() == 1;
                
                if (exists)
                {
                    logger.LogInformation("{TableName} table exists.", tableName);
                }
                else
                {
                    logger.LogWarning("{TableName} table does not exist.", tableName);
                }
                
                return exists;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking if {TableName} table exists.", tableName);
                throw;
            }
        }
        
        private static void CreateStudentTable(StudentsAPIContext context, ILogger logger)
        {
            try
            {
                var createTableSql = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Student')
                    BEGIN
                        CREATE TABLE Student (
                            ID INT PRIMARY KEY IDENTITY(1,1),
                            LastName NVARCHAR(50) NOT NULL,
                            FirstMidName NVARCHAR(50) NOT NULL,
                            EnrollmentDate DATETIME NOT NULL
                        );
                    END";
                
                context.Database.ExecuteSqlRaw(createTableSql);
                logger.LogInformation("Student table created successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create Student table.");
                throw;
            }
        }
        
        private static void CreateEnrollmentTable(StudentsAPIContext context, ILogger logger)
        {
            try
            {
                var createTableSql = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Enrollment')
                    BEGIN
                        CREATE TABLE Enrollment (
                            EnrollmentID INT PRIMARY KEY IDENTITY(1,1),
                            CourseID INT NOT NULL,
                            StudentID INT NOT NULL,
                            Title NVARCHAR(50),
                            Credits INT,
                            Grade INT,
                            FOREIGN KEY (StudentID) REFERENCES Student(ID)
                        );
                    END";
                
                context.Database.ExecuteSqlRaw(createTableSql);
                logger.LogInformation("Enrollment table created successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create Enrollment table.");
                throw;
            }
        }
    }
}
