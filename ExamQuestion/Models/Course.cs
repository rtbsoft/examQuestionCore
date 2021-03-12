using System.ComponentModel.DataAnnotations.Schema;

namespace ExamQuestion.Models
{
    public enum Semesters
    {
        Fall,
        Winter,
        Spring
    }

    public class Course
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public int Year { get; set; }

        public Semesters Semester { get; set; }

        [ForeignKey("User")] public int UserId { get; set; }

        public User User { get; set; }
    }
}