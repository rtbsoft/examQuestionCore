using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExamQuestion.Models
{
    public class Assignment
    {
        public int Id { get; set; }
        public DateTime Downloaded { get; set; }
        public string Ip { get; set; }

        [ForeignKey("StudentId")] public int StudentId { get; set; }

        public Student Student { get; set; }

        [ForeignKey("DocumentId")] public int DocumentId { get; set; }

        public Document Document { get; set; }

        public override bool Equals(object obj)
        {
            var a = obj as Assignment;
            return DocumentId == a?.DocumentId && StudentId == a.StudentId;
        }

        public override int GetHashCode() => HashCode.Combine(DocumentId, StudentId);
    }
}