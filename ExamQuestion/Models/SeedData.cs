using System;
using System.Linq;

using ExamQuestion.Utils;

using Microsoft.Extensions.DependencyInjection;

namespace ExamQuestion.Models
{
    public static class SeedData
    {
        public static void CreateSeedData(IServiceProvider serviceProvider)
        {
            using var serviceScope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var context = serviceScope.ServiceProvider.GetService<AppDbContext>();

            if (context == null)
                return;

            if (!context.Schools.Any())
            {
                context.Schools.Add(new School {Name = "Conestoga College"});
                context.SaveChanges();
            }

            var schoolId = context.Schools.FirstOrDefault(s => s.Name == "Conestoga College")?.Id ?? 0;

            if (!context.Users.Any())
            {
                context.Users.Add(new User
                {
                    Email = "fred@smith.co",
                    Name = "Fred Smith",
                    Password = PasswordHash.HashPassword("123"),
                    SchoolId = schoolId
                });
                context.SaveChanges();
            }

            var userId = context.Users.FirstOrDefault(u => u.Email == "fred@smith.co")?.Id ?? 0;

            if (!context.Courses.Any())
            {
                context.Courses.Add(new Course
                {
                    Name = "PROG8185", Semester = Semesters.Fall, UserId = userId, Year = 2020
                });
                context.SaveChanges();
            }

            var courseId = context.Courses.FirstOrDefault(u => u.Name == "PROG8185")?.Id ?? 0;

            if (context.Exams.Any())
            {
                var exam = context.Exams.FirstOrDefault(u => u.Name == "Midterm");
                if (exam != null)
                {
                    exam.Start = DateTime.UtcNow.AddDays(value: 10);
                    exam.DurationMinutes = 120;
                    context.SaveChanges();
                }
            }

            if (!context.Exams.Any())
            {
                context.Exams.Add(new Exam
                {
                    CourseId = courseId,
                    AuthenticationCode = "abc",
                    Start = DateTime.UtcNow.AddDays(value: 10),
                    DurationMinutes = 120,
                    Name = "Midterm"
                });
                context.SaveChanges();
            }

            var examId = context.Exams.FirstOrDefault(u => u.Name == "Midterm")?.Id ?? 0;

            if (!context.Questions.Any())
            {
                context.Questions.Add(new Question {ExamId = examId, Description = "Question 1"});
                context.SaveChanges();
            }

            var questionId = context.Questions.FirstOrDefault(u => u.Description == "Question 1")?.Id ?? 0;

            if (!context.Documents.Any())
            {
                context.Documents.AddRange(
                    new Document
                    {
                        QuestionId = questionId,
                        Url = "https://1drv.ms/w/s!Ai94da6gxaGUqT-cwivdG_NRO6sV?e=ILMirz",
                        PublicFileName = "exam1e1.doc"
                    },
                    new Document
                    {
                        QuestionId = questionId,
                        Url = "https://1drv.ms/w/s!Ai94da6gxaGUqUDsvafNxdAPng4q?e=bs76VF",
                        PublicFileName = "exam 2012.docx"
                    });
                context.SaveChanges();
            }

            var document1Id = context.Documents.FirstOrDefault(u => u.PublicFileName == "exam1e1.doc")?.Id ?? 0;

            if (!context.Students.Any())
            {
                context.Students.Add(new Student {CourseId = courseId, Name = "Joe Student", Number = "123"});
                context.SaveChanges();
            }

            var studentId = context.Students.FirstOrDefault(u => u.Name == "Joe Student")?.Id ?? 0;

            if (!context.Assignments.Any())
            {
                context.Assignments.Add(new Assignment
                {
                    DocumentId = document1Id,
                    StudentId = studentId,
                    Downloaded = DateTime.UtcNow,
                    Ip = "192.168.0.1"
                });
                context.SaveChanges();
            }
        }
    }
}