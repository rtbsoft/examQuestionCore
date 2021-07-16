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
    public class DocumentController : ControllerBase
    {
        //remember access to the database
        private readonly AppDbContext db;
        private readonly ILogger<DocumentController> logger;

        //constructor
        public DocumentController(AppDbContext db, ILogger<DocumentController> logger)
        {
            //remember the database
            this.db = db;
            this.logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Document>>> Get()
        {
            ActionResult<IEnumerable<Document>> ar;

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    var documents = await db.Documents.Where(d => db.Questions.Any(q =>
                            q.Id == d.QuestionId && db.Exams.Any(e =>
                                e.Id == q.ExamId && db.Courses.Any(c => c.Id == e.CourseId && c.UserId == userId))))
                        .ToListAsync();

                    logger.LogTrace($"Found {documents.Count} documents for user {userId}");
                    ar = documents;
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

        // POST api/Document
        [HttpPost]
        public async Task<IdResponse> Post([FromBody] Document document)
        {
            var resp = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    //make sure this user owns the question they want to attach a document to
                    var ownsQuestion = await doesOwnQuestion(document.QuestionId, userId);
                    if (ownsQuestion)
                    {
                        if (!string.IsNullOrWhiteSpace(document.PublicFileName) &&
                            !string.IsNullOrWhiteSpace(document.Url))
                        {
                            // ReSharper disable once MethodHasAsyncOverload
                            db.Documents.Add(document);
                            await db.SaveChangesAsync();
                            resp.Id = document.Id;

                            logger.LogTrace($"Added {document.PublicFileName} to question {document.QuestionId}");
                        }
                        else
                        {
                            logger.LogWarning($"Invalid properties in {document}");
                            resp.ResponseCodes.Add(ResponseCodes.InvalidDocumentFields);
                        }
                    }
                    else
                    {
                        logger.LogWarning($"User {userId} does not own question {document.QuestionId}");
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
                logger.LogError(ex,
                    $"Failed to add {document.PublicFileName} with link {document.Url} to question {document.QuestionId}");
                resp.ResponseCodes.Add(ResponseCodes.InternalError);
            }

            return resp;
        }

        /* if we want to allow file uploads
                    if (file != null && ownsQuestion)
                    {
                        var doc = new Document
                        {
                            QuestionId = questionId,
                            PrivateFileName = Path.GetRandomFileName(),
                            PublicFileName = file.FileName,
                            ContentType = file.ContentType
                        };

                        var filePath = Path.Combine(Util.GetDocumentPath(), doc.PrivateFileName);
                        await using (var stream = new FileStream(filePath, FileMode.Create))
                            await file.CopyToAsync(stream);

                        // ReSharper disable once MethodHasAsyncOverload
                        db.Documents.Add(doc);
                        await db.SaveChangesAsync();
                        resp.Id = doc.Id;

                        logger.LogTrace($"Added {doc.PrivateFileName} {doc.PublicFileName} to question {questionId}");
                    }
                    else
                        logger.LogWarning(
                            $"{userId} {(file == null ? "no file" : "")} {(ownsQuestion ? "" : "not owner")}");
         */

        // PUT api/Document/5
        [HttpPut("{id}")]
        public async Task<IdResponse> Put(int id, [FromBody] Document newDoc)
        {
            var resp = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id);
                    if (doc != null)
                    {
                        //make sure this user owns the question they want to attach a document to
                        var ownsQuestion = await doesOwnQuestion(doc.QuestionId, userId);

                        if (ownsQuestion && !string.IsNullOrWhiteSpace(newDoc.PublicFileName))
                        {
                            doc.PublicFileName = newDoc.PublicFileName;
                            doc.Url = newDoc.Url;
                            await db.SaveChangesAsync();
                            resp.Id = doc.Id;

                            logger.LogTrace($"updated {id} with name {doc.PublicFileName}");
                        }
                        else
                        {
                            logger.LogWarning(
                                $"{userId} {(ownsQuestion ? "" : "not owner")} invalid new name: {newDoc.PublicFileName}");
                            resp.ResponseCodes.Add(string.IsNullOrWhiteSpace(newDoc.PublicFileName)
                                ? ResponseCodes.EmptyDocumentName
                                : ResponseCodes.InvalidUser);
                        }
                    }
                    else
                    {
                        logger.LogWarning($"document {id} not found");
                        resp.ResponseCodes.Add(ResponseCodes.InvalidDocumentFields);
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

        // DELETE api/Document/5
        [HttpDelete("{id}")]
        public async Task<IdResponse> Delete(int id)
        {
            var resp = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id);
                    if (doc != null)
                    {
                        //make sure this user owns the question they want to attach a document to
                        if (await doesOwnQuestion(doc.QuestionId, userId))
                        {
                            //delete the record from the db
                            db.Documents.Remove(doc);
                            await db.SaveChangesAsync();
                            resp.Id = doc.Id;

                            logger.LogTrace($"deleted {id}");
                        }
                        else
                        {
                            logger.LogWarning($"{userId} not owner of document {id}");
                            resp.ResponseCodes.Add(ResponseCodes.InvalidUser);
                        }
                    }
                    else
                    {
                        logger.LogWarning($"document {id} not found");
                        resp.Id = 1;
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
                    resp.ResponseCodes.Add(ResponseCodes.DocumentInUse);
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

        private async Task<bool> doesOwnQuestion(int questionId, int userId) =>
            await db.Questions.AnyAsync(q => q.Id == questionId && db.Exams.Any(e =>
                q.ExamId == e.Id && db.Courses.Any(c =>
                    e.CourseId == c.Id && c.UserId == userId)));
    }
}