using System.ComponentModel.DataAnnotations.Schema;

namespace ExamQuestion.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }

        [ForeignKey("School")] public int? SchoolId { get; set; }
        public School School { get; set; }

        public override string ToString() => $"{Id} | {Name} | {SchoolId} | {Email} | {Password}";
    }
}