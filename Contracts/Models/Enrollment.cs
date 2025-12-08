namespace Contracts.Models
{
    public enum Grade
    {
        A, B, C, D, F
    }
    public class Enrollment
    {
        public int EnrollmentID { get; set; }
        public int CourseID { get; set; }
        public required string Title { get; set; }
        public int Credits { get; set; }
        public int StudentID { get; set; }
        public Grade? Grade { get; set; }
        public Student? Student { get; set; }
    }

}
