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
    public class QuestionController : ControllerBase
    {
        //remember access to the database
        private readonly AppDbContext db;
        private readonly ILogger<QuestionController> logger;

        //constructor
        public QuestionController(AppDbContext db, ILogger<QuestionController> logger)
        {
            //remember the database
            this.db = db;
            this.logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Question>>> Get()
        {
            ActionResult<IEnumerable<Question>> ar;

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    var questions = await db.Questions.Where(q => db.Exams.Any(e =>
                        e.Id == q.ExamId && db.Courses.Any(c => e.CourseId == c.Id && c.UserId == userId)))
                        .ToListAsync();

                    logger.LogTrace($"Found {questions.Count} questions for user {userId}");
                    ar = questions;
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

        // POST api/Question
        [HttpPost]
        public async Task<IdResponse> Post([FromBody] Question question)
        {
            var resp = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    //make sure this user owns the question they want to attach a document to
                    var ownsExam = await doesOwnExam(question.ExamId, userId);

                    if (ownsExam && !string.IsNullOrWhiteSpace(question.Description))
                    {
                        // ReSharper disable once MethodHasAsyncOverload
                        db.Questions.Add(question);
                        await db.SaveChangesAsync();
                        resp.Id = question.Id;

                        logger.LogTrace($"Added {question.Description} to exam {question.ExamId}");
                    }
                    else
                    {
                        logger.LogWarning($"{userId} not owner. Description: {question.Description}");
                        resp.ResponseCodes.Add(ownsExam
                            ? ResponseCodes.InvalidQuestionFields
                            : ResponseCodes.InvalidUser);
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
                logger.LogError(ex, $"Failed to add {question.Description} to exam {question.ExamId}");
                resp.ResponseCodes.Add(ResponseCodes.InternalError);
            }

            return resp;
        }

        // PUT api/Question/5
        [HttpPut("{id}")]
        public async Task<IdResponse> Put(int id, [FromBody] Question newQuestion)
        {
            var resp = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    var question = await db.Questions.FirstOrDefaultAsync(q => q.Id == id);
                    if (question != null)
                    {
                        //make sure this user owns the question they want to attach a document to
                        var ownsExam = await doesOwnExam(question.ExamId, userId);

                        if (ownsExam && !string.IsNullOrWhiteSpace(newQuestion.Description))
                        {
                            question.Description = newQuestion.Description;
                            await db.SaveChangesAsync();
                            resp.Id = question.Id;

                            logger.LogTrace($"updated {id} with name {question.Description}");
                        }
                        else
                        {
                            logger.LogWarning(
                                $"{userId} {(ownsExam ? "" : "not owner")} invalid new description: {newQuestion.Description}");
                            resp.ResponseCodes.Add(ownsExam
                                ? ResponseCodes.InvalidQuestionFields
                                : ResponseCodes.InvalidUser);
                        }
                    }
                    else
                    {
                        logger.LogWarning($"question {id} not found");
                        resp.ResponseCodes.Add(ResponseCodes.InvalidQuestionFields);
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

        // DELETE api/Question/5
        [HttpDelete("{id}")]
        public async Task<IdResponse> Delete(int id)
        {
            var resp = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    var question = await db.Questions.FirstOrDefaultAsync(q => q.Id == id);
                    if (question != null)
                    {
                        //make sure this user owns the question they want to attach a document to
                        if (await doesOwnExam(question.ExamId, userId))
                        {
                            //delete the record from the db
                            db.Questions.Remove(question);
                            await db.SaveChangesAsync();
                            resp.Id = question.Id;

                            logger.LogTrace($"deleted {id}");
                        }
                        else
                        {
                            resp.ResponseCodes.Add(ResponseCodes.InvalidUser);
                            logger.LogWarning($"{userId} not owner of question {id}");
                        }
                    }
                    else
                    {
                        resp.Id = 1;
                        logger.LogWarning($"question {id} not found");
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
                    resp.ResponseCodes.Add(ResponseCodes.QuestionInUse);
                    logger.LogWarning("Attempt to delete record in use");
                }
                else
                {
                    resp.ResponseCodes.Add(ResponseCodes.InternalError);
                    logger.LogError(ex, $"failed to delete {id}");
                }
            }

            return resp;
        }

        private async Task<bool> doesOwnExam(int examId, int userId) =>
            await db.Exams.AnyAsync(
                e => examId == e.Id && db.Courses.Any(c => e.CourseId == c.Id && c.UserId == userId));
    }
}