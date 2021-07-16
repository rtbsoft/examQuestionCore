using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ExamQuestion.Hubs;
using ExamQuestion.Models;
using ExamQuestion.Utils;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExamQuestion.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AssignmentController : ControllerBase
    {
        private static readonly SemaphoreSlim slim = new SemaphoreSlim(initialCount: 1, maxCount: 1);

        //remember access to the database
        private readonly AppDbContext db;
        private readonly IHubContext<AllocationHub, IAllocationClient> hub;
        private readonly ILogger<AssignmentController> logger;

        private readonly Random random;

        //constructor
        public AssignmentController(AppDbContext db, IHubContext<AllocationHub, IAllocationClient> hub,
            ILogger<AssignmentController> logger)
        {
            //remember the database
            this.db = db;
            this.hub = hub;
            this.logger = logger;
            random = new Random();
        }

        // GET: api/Assignment
        // let the user see who was assigned what documents for a specific exam
        [HttpGet("Exam/{examId}")]
        public async Task<ActionResult<List<Assignment>>> Get(int examId)
        {
            ActionResult<List<Assignment>> ar;

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    //if the logged in user owns this exam
                    var exam = await db.Exams.FirstOrDefaultAsync(e =>
                        e.Id == examId && db.Courses.Any(c => e.CourseId == c.Id && c.UserId == userId));
                    if (exam != null)
                    {
                        //get all the assignments that have documents that have questions associated with this exam
                        var assignments = await db.Assignments.Where(a => db.Documents.Any(d =>
                                d.Id == a.DocumentId &&
                                db.Questions.Any(q => q.Id == d.QuestionId && q.ExamId == exam.Id)))
                            .ToListAsync();
                        ar = assignments.ToList();
                    }
                    else
                    {
                        logger.LogWarning($"Exam {examId} does not belong to {userId}");
                        ar = BadRequest();
                    }
                }
                else
                {
                    logger.LogWarning("Attempt without logging in");
                    ar = Unauthorized();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"examId: {examId}");
                ar = StatusCode(statusCode: 500);
            }

            return ar;
        }

        //GET: api/Assignment/Export
        [HttpGet("Export/{examId}")]
        public async Task<IActionResult> ExportAssignment(int examId)
        {
            IActionResult resp = NotFound();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    //if the logged in user owns this exam
                    var exam = await db.Exams.FirstOrDefaultAsync(e =>
                        e.Id == examId && db.Courses.Any(c => e.CourseId == c.Id && c.UserId == userId));
                    if (exam != null)
                        //get all the assignments that have documents that have questions associated with this exam
                        //create the csv column header - assume that all students are assigned the same number of questions
                        resp = File(
                            Encoding.UTF8.GetBytes(getStudentAssignmentCsv(await getAllAssignmentsByStudent(exam.Id))),
                            "text/csv", $"{exam.Name}.csv");
                    else
                    {
                        logger.LogWarning($"Exam {examId} does not belong to {userId}");
                        resp = BadRequest();
                    }
                }
                else
                {
                    logger.LogWarning("Attempt without logging in");
                    resp = Unauthorized();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, examId.ToString());
                resp = StatusCode(statusCode:500);
            }

            return resp;
        }


        //POST: api/Assignment/Student
        [HttpPost("Student")]
        public async Task<IActionResult> GetAssignment([FromBody] AssignRequest assignRequest)
        {
            IActionResult resp = NotFound();

            try
            {
                var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == assignRequest.ExamId);
                var student = await db.Students.FirstOrDefaultAsync(s => s.Id == assignRequest.StudentId);
                var now = DateTime.UtcNow;

                if (exam != null && exam.AuthenticationCode == assignRequest.AuthenticationCode && student != null &&
                    student.Number == assignRequest.StudentNumber &&
                    (!exam.IsLimitedAccess || exam.IsLimitedAccess && exam.Start <= now &&
                        exam.Start.AddMinutes(exam.DurationMinutes) >= now))
                {
                    //ok, we believe that you are one of the students who should get a set of documents from this exam (one per question)
                    var documents = await getDocumentsForStudent(exam, student,
                        HttpContext.Connection.RemoteIpAddress?.ToString());

                    //notify prof that a student has been allocated something
                    await sendNotification(exam, student, documents);

                    //zip the files
                    var ms = await AssignmentHandler.CreateZipArchive(documents, student.Name);
                    resp = File(ms.ToArray(), "application/zip", $"{student.Name}.zip");

                    logger.LogTrace(
                        $"{assignRequest} fulfilled with {string.Join(",", documents.Select(d => d.Id).ToList())}");
                }
                else
                {
                    logger.LogWarning($"{assignRequest} not a match or too early {exam?.IsLimitedAccess}");
                    if (exam?.IsLimitedAccess ?? false)
                        resp = BadRequest();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, assignRequest.ToString());
                resp = StatusCode(statusCode:500);
            }

            return resp;
        }

        #region private functions

        private async Task<Dictionary<int, List<Assignment>>> getAllAssignmentsByStudent(int examId)
        {
            var assignments = await db.Assignments
                .Where(a => db.Documents.Any(d =>
                    d.Id == a.DocumentId && db.Questions.Any(q => q.Id == d.QuestionId && q.ExamId == examId)))
                .Include(a => a.Student).Include(a => a.Document).ThenInclude(d => d.Question)
                .OrderBy(a => a.Downloaded).ToListAsync();

            //see what documents each student received
            var byStudent = new Dictionary<int, List<Assignment>>();
            foreach (var assign in assignments)
                if (byStudent.TryGetValue(assign.StudentId, out var a))
                {
                    if (a.All(d => d.DocumentId != assign.DocumentId))
                        a.Add(assign);
                }
                else byStudent.Add(assign.StudentId, new List<Assignment> {assign});

            return byStudent;
        }

        private string getStudentAssignmentCsv(Dictionary<int, List<Assignment>> byStudent)
        {
            var csv = "Student, IP, Time,";
            foreach (var a in byStudent.Values.First())
                csv += $"\"{a.Document.Question.Description}\",";
            csv += Environment.NewLine;

            //add a record - assume student does not move IP addresses and 
            foreach (var a in byStudent.Values)
            {
                var first = a.First();
                csv += $"\"{first.Student.Name}\",{first.Ip},\"{first.Downloaded}\",";
                foreach (var d in a)
                    csv += $"{d.Document.PublicFileName},";
                csv += Environment.NewLine;
            }

            return csv;
        }

        private async Task saveDocuments(Student student, string ip, List<Document> documents)
        {
            foreach (var document in documents)
                // ReSharper disable once MethodHasAsyncOverload
                db.Assignments.Add(new Assignment
                {
                    StudentId = student.Id, DocumentId = document.Id, Downloaded = DateTime.UtcNow, Ip = ip
                });

            await db.SaveChangesAsync();
        }

        private async Task sendNotification(Exam exam, Student student, List<Document> documents)
        {
            var userId = (await db.Courses.FirstOrDefaultAsync(c => c.Id == exam.CourseId)).UserId;
            var numDownloads = 0;
            if (documents.Count > 0)
                numDownloads = await db.Assignments.CountAsync(a =>
                    a.StudentId == student.Id && a.DocumentId == documents[0].Id);

            await hub.Clients.Groups(userId.ToString()).DocumentsAllocated(new AllocatedMessage
            {
                NumDownloads = numDownloads,
                ExamId = exam.Id,
                CourseName = (await db.Courses.FirstOrDefaultAsync(c => c.Id == exam.CourseId)).Name,
                ExamName = exam.Name,
                StudentName = student.Name,
                DocumentNames = string.Join(",", documents.Select(d => d.PublicFileName))
            });
        }

        private async Task<List<Document>> getDocumentsForStudent(Exam exam, Student student, string ip)
        {
            List<Document> documents;

            if (!await db.Assignments.AnyAsync(a => a.StudentId == student.Id && db.Documents.Any(d =>
                d.Id == a.DocumentId && db.Questions.Any(q =>
                    q.Id == d.QuestionId && q.ExamId == exam.Id))))
            {
                //we need to do this in one student at a time, so if multiple students hit the server
                //at the same time, we'll still process them sequentially
                await slim.WaitAsync();

                try
                {
                    //now that we know we haven't already given you documents
                    //figure out which documents to hand over
                    documents = await AssignmentHandler.GetStudentDocuments(db, student, exam, ip, random);
                    await saveDocuments(student, ip, documents);
                }
                finally
                {
                    slim.Release();
                }
            }
            else
            {
                //send them the same files again
                documents = await db.Documents.Where(d => db.Assignments.Any(a =>
                    a.DocumentId == d.Id && a.StudentId == student.Id && db.Questions.Any(q =>
                        d.QuestionId == q.Id && db.Exams.Any(e => e.Id == q.ExamId && e.Id == exam.Id)))).ToListAsync();

                await saveDocuments(student, ip, documents);
            }

            return documents;
        }

        #endregion
    }
}