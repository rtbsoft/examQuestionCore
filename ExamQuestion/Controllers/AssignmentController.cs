using System;
using System.Collections.Generic;
using System.Linq;
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
        private static readonly SemaphoreSlim slim = new SemaphoreSlim(1, 1);

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
        public async Task<IEnumerable<Assignment>> Get(int examId)
        {
            IEnumerable<Assignment> assignments = Array.Empty<Assignment>();

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
                        assignments = await db.Assignments.Where(a => db.Documents.Any(d =>
                                d.Id == a.DocumentId &&
                                db.Questions.Any(q => q.Id == d.QuestionId && q.ExamId == exam.Id)))
                            .ToListAsync();
                    else
                        logger.LogWarning($"Exam {examId} does not belong to {userId}");
                }
                else
                    logger.LogWarning("Attempt without logging in");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"examId: {examId}");
            }

            return assignments;
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

                if (exam != null && exam.AuthenticationCode == assignRequest.AuthenticationCode &&
                    student != null && student.Number == assignRequest.StudentNumber &&
                    (!exam.IsLimitedAccess || exam.IsLimitedAccess && exam.Start <= now &&
                        exam.Start.AddMinutes(exam.DurationMinutes) >= now)
                )
                {
                    List<Document> documents;

                    var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

                    //ok, we believe that you are one of the students who should get a set of documents from this exam (one per question)
                    if (!await db.Assignments.AnyAsync(a =>
                        a.StudentId == student.Id && db.Documents.Any(d =>
                            d.Id == a.DocumentId &&
                            db.Questions.Any(q => q.Id == d.QuestionId && q.ExamId == exam.Id))))
                    {
                        //we need to do this in one student at a time, so if multiple students hit the server
                        //at the same time, we'll still process them sequentially
                        await slim.WaitAsync();

                        try
                        {
                            //now that we know we haven't already given you documents
                            //figure out which documents to hand over
                            documents = await AssignmentHandler.GetStudentDocuments(db, student, exam, ip, random);

                            foreach (var document in documents)
                                // ReSharper disable once MethodHasAsyncOverload
                                db.Assignments.Add(new Assignment
                                {
                                    StudentId = student.Id,
                                    DocumentId = document.Id,
                                    Downloaded = DateTime.UtcNow,
                                    Ip = ip
                                });

                            await db.SaveChangesAsync();
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
                                    d.QuestionId == q.Id && db.Exams.Any(e => e.Id == q.ExamId && e.Id == exam.Id))))
                            .ToListAsync();

                        foreach (var document in documents)
                            // ReSharper disable once MethodHasAsyncOverload
                            db.Assignments.Add(new Assignment
                            {
                                StudentId = student.Id,
                                DocumentId = document.Id,
                                Downloaded = DateTime.UtcNow,
                                Ip = ip
                            });

                        await db.SaveChangesAsync();
                    }

                    //notify prof that a student has been allocated something
                    var userId = (await db.Courses.FirstOrDefaultAsync(c => c.Id == exam.CourseId)).UserId;
                    await hub.Clients.Groups(userId.ToString())
                        .DocumentsAllocated(new AllocatedMessage
                        {
                            NumDownloads = await db.Assignments.CountAsync(a => a.StudentId == student.Id && a.DocumentId == documents[0].Id),
                            ExamId = exam.Id,
                            CourseName = (await db.Courses.FirstOrDefaultAsync(c => c.Id == exam.CourseId)).Name,
                            ExamName = exam.Name,
                            StudentName = student.Name,
                            DocumentNames = string.Join(",", documents.Select(d => d.PublicFileName))
                        });

                    //zip the files
                    var ms = await AssignmentHandler.CreateZipArchive(documents, student.Name);
                    resp = File(ms.ToArray(), "application/zip", $"{student.Name}.zip");

                    var docList = string.Join(",", documents.Select(d => d.Id).ToList());
                    logger.LogTrace($"{assignRequest} fulfilled with {docList}");
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
            }

            return resp;
        }
    }
}