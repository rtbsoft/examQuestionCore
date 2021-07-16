using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExamQuestion.Models;
using ExamQuestion.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExamQuestion.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExamController : ControllerBase
    {
        //remember access to the database
        private readonly AppDbContext db;
        private readonly ILogger<ExamController> logger;

        //constructor
        public ExamController(AppDbContext db, ILogger<ExamController> logger)
        {
            //remember the database
            this.db = db;
            this.logger = logger;
        }

        // GET: api/Exam
        //get the list of exams for a user
        //this should always work, as students will need this for finding their exam
        //note that we don't return the AuthenticationCode in this call, as this is a secret
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Exam>>> Get()
        {
            ActionResult<IEnumerable<Exam>> ar;

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    var exams = await db.Exams.Where(e => db.Courses.Any(c => c.Id == e.CourseId && c.UserId == userId))
                        .ToListAsync();

                    logger.LogTrace($"Found {exams.Count} exams for user {userId}");
                    ar = exams;
                }
                else
                    ar = Unauthorized();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "");
                ar = StatusCode(statusCode: 500);
            }

            return ar;
        }

        // GET: api/Exam
        //get the list of exams for a course
        //this should always work, as students will need this for finding their exam
        //note that we don't return the AuthenticationCode in this call, as this is a secret
        [HttpGet("course/{courseId}")]
        public async Task<ActionResult<IEnumerable<Exam>>> GetForCourse(int courseId)
        {
            ActionResult<IEnumerable<Exam>> ar;

            try
            {
                ar = await db.Exams.Where(e => e.CourseId == courseId).Select(e => new Exam
                {
                    Id = e.Id,
                    Name = e.Name,
                    Start = e.Start,
                    DurationMinutes = e.DurationMinutes,
                    CourseId = e.CourseId
                }).ToListAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "");
                ar = StatusCode(statusCode: 500);
            }

            return ar;
        }

        // POST api/Exam
        [HttpPost]
        public async Task<IdResponse> Post([FromBody] Exam exam)
        {
            var resp = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    //make sure this user owns the question they want to attach a document to
                    var ownsCourse = await doesOwnCourse(exam.CourseId, userId);

                    if (ownsCourse && !string.IsNullOrWhiteSpace(exam.AuthenticationCode) &&
                        !string.IsNullOrWhiteSpace(exam.Name) && exam.Start > DateTime.UtcNow)
                    {
                        // ReSharper disable once MethodHasAsyncOverload
                        db.Exams.Add(exam);
                        await db.SaveChangesAsync();
                        resp.Id = exam.Id;

                        logger.LogTrace($"Added exam {exam.Name} to course {exam.CourseId}");
                    }
                    else
                    {
                        logger.LogWarning($"{userId} {(ownsCourse ? "" : "not")} owner; fields {exam}");
                        if (ownsCourse)
                        {
                            if (exam.Start < DateTime.UtcNow)
                                resp.ResponseCodes.Add(ResponseCodes.InvalidExamStart);
                            if (string.IsNullOrWhiteSpace(exam.AuthenticationCode) ||
                                string.IsNullOrWhiteSpace(exam.Name))
                                resp.ResponseCodes.Add(ResponseCodes.InvalidExamFields);
                        }
                        else
                            resp.ResponseCodes.Add(ResponseCodes.InvalidUser);
                    }
                }
                else
                {
                    logger.LogWarning("Attempt without logging in");
                    resp.ResponseCodes.Add(ResponseCodes.NotLoggedIn);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to add {exam.Name} to course {exam.CourseId}");
                resp.ResponseCodes.Add(ResponseCodes.InternalError);
            }

            return resp;
        }

        // PUT api/Exam
        [HttpPut("{id}")]
        public async Task<IdResponse> Put(int id, [FromBody] Exam newExam)
        {
            var resp = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == id);
                    if (exam != null)
                    {
                        //make sure this user owns the question they want to attach a document to
                        var ownsCourse = await doesOwnCourse(exam.CourseId, userId);

                        if (ownsCourse && !string.IsNullOrWhiteSpace(newExam.AuthenticationCode) &&
                            !string.IsNullOrWhiteSpace(newExam.Name) && newExam.Start > DateTime.UtcNow)
                        {
                            exam.AuthenticationCode = newExam.AuthenticationCode;
                            exam.DurationMinutes = newExam.DurationMinutes;
                            exam.Name = newExam.Name;
                            exam.Start = newExam.Start;
                            exam.IsLimitedAccess = newExam.IsLimitedAccess;
                            await db.SaveChangesAsync();

                            resp.Id = exam.Id;

                            logger.LogTrace($"updated {id} with name {exam.Name}");
                        }
                        else
                        {
                            logger.LogWarning($"{userId} {(ownsCourse ? "" : "not")} owner or invalid data {exam}");
                            if (ownsCourse)
                            {
                                if (exam.Start < DateTime.UtcNow)
                                    resp.ResponseCodes.Add(ResponseCodes.InvalidExamStart);
                                if (string.IsNullOrWhiteSpace(exam.AuthenticationCode) ||
                                    string.IsNullOrWhiteSpace(exam.Name))
                                    resp.ResponseCodes.Add(ResponseCodes.InvalidExamFields);
                            }
                            else
                                resp.ResponseCodes.Add(ResponseCodes.InvalidUser);
                        }
                    }
                    else
                    {
                        logger.LogWarning($"exam {id} not found");
                        resp.ResponseCodes.Add(ResponseCodes.InvalidExamFields);
                    }
                }
                else
                {
                    logger.LogWarning("Attempt without logging in");
                    resp.ResponseCodes.Add(ResponseCodes.NotLoggedIn);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"failed to edit {id}");
                resp.ResponseCodes.Add(ResponseCodes.InternalError);
            }

            return resp;
        }

        // DELETE api/Exam/5
        [HttpDelete("{id}")]
        public async Task<IdResponse> Delete(int id)
        {
            var resp = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == id);
                    if (exam != null)
                    {
                        //make sure this user owns the exam they want to delete
                        if (await doesOwnCourse(exam.CourseId, userId))
                        {
                            db.Exams.Remove(exam);
                            await db.SaveChangesAsync();
                            resp.Id = exam.Id;

                            logger.LogTrace($"deleted {id}");
                        }
                        else
                        {
                            logger.LogWarning($"{userId} not owner of exam {id}");
                            resp.ResponseCodes.Add(ResponseCodes.InvalidExamFields);
                        }
                    }
                    else
                    {
                        resp.Id = 1;
                        logger.LogWarning($"exam {id} not found");
                    }
                }
                else
                {
                    logger.LogWarning("Attempt without logging in");
                    resp.ResponseCodes.Add(ResponseCodes.NotLoggedIn);
                }
            }
            catch (Exception ex)
            {
                if (ex.InnerException is SqlException exception && exception.Number == 547)
                {
                    logger.LogWarning("Attempt to delete record in use");
                    resp.ResponseCodes.Add(ResponseCodes.ExamInUse);
                }
                else
                {
                    logger.LogError(ex, $"failed to delete {id}");
                    resp.ResponseCodes.Add(ResponseCodes.InternalError);
                }
            }

            return resp;
        }

        private async Task<bool> doesOwnCourse(int courseId, int userId) =>
            await db.Courses.AnyAsync(c => c.Id == courseId && c.UserId == userId);
    }
}