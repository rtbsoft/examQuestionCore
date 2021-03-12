namespace ExamQuestion.Models
{
    public class AssignRequest
    {
        public int StudentId { get; set; }
        public string StudentNumber { get; set; }
        public string AuthenticationCode { get; set; }
        public int ExamId { get; set; }

        public override string ToString() => $"{StudentId} | {StudentNumber} | {AuthenticationCode} | {ExamId}";
    }
}