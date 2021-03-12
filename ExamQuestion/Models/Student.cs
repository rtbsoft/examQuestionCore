using System.ComponentModel.DataAnnotations.Schema;

namespace ExamQuestion.Models
{
    public class Student
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Number { get; set; }
        public string Email { get; set; }

        [ForeignKey("Course")] public int CourseId { get; set; }

        public Course Course { get; set; }

        public override string ToString() => $"{Id} | {Name} | {Number}";
    }
}