using System.ComponentModel.DataAnnotations.Schema;

namespace ExamQuestion.Models
{
    public class Document
    {
        public int Id { get; set; }
        public string PublicFileName { get; set; }
        public string Url { get; set; }

        [ForeignKey("Question")] public int QuestionId { get; set; }

        public Question Question { get; set; }
    }
}