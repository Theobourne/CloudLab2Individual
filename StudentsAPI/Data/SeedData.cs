using Microsoft.EntityFrameworkCore;
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
                context.Database.EnsureCreated();
                // Look for any Student.
                if (context.Student.Any())
                {
                    return;   // DB has been seeded
                }
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
            }
        }
    }

}
