using System.ComponentModel.DataAnnotations.Schema;

namespace ExamQuestion.Models
{
    public class Question
    {
        public int Id { get; set; }
        public string Description { get; set; }

        [ForeignKey("Exam")] public int ExamId { get; set; }

        public Exam Exam { get; set; }
    }
}