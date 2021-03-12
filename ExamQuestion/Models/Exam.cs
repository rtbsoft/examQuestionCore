using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExamQuestion.Models
{
    public class Exam
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime Start { get; set; }
        public int DurationMinutes { get; set; }
        public string AuthenticationCode { get; set; }
        public bool IsLimitedAccess { get; set; }

        [ForeignKey("Course")] public int CourseId { get; set; }

        public Course Course { get; set; }

        public override string ToString() => $"{Id}:{Name}:{Start}:{DurationMinutes}:{AuthenticationCode}";
    }
}