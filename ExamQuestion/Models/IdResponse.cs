using System.Collections.Generic;

namespace ExamQuestion.Models
{
    public enum ResponseCodes
    {
        EmptyStudentFields,
        NotLoggedIn,
        StudentInUse,
        InvalidEmail,
        InvalidName,
        InvalidSchool,
        InvalidUser,
        InvalidPassword,
        InvalidCredentials,
        EmailInUse,
        IncorrectCredentials,
        DeleteOtherUser,
        UserInUse,
        InvalidCourseFields,
        InvalidCourseName,
        NoEditOldCourse,
        CourseInUse,
        InvalidDocumentFields,
        EmptyDocumentName,
        DocumentInUse,
        InvalidExamStart,
        InvalidExamFields,
        ExamInUse,
        InvalidQuestionFields,
        QuestionInUse,
        InvalidSchoolFields,
        SchoolInUse,
        WeakPassword
    }

    public class IdResponse
    {
        public int Id { get; set; }
        public List<ResponseCodes> ResponseCodes { get; set; } = new List<ResponseCodes>();
    }
}