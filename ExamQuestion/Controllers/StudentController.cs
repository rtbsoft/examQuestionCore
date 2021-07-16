using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ExamQuestion.Models;
using ExamQuestion.Utils;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExamQuestion.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StudentController : ControllerBase
    {
        //remember access to the database
        private readonly AppDbContext db;
        private readonly ILogger<StudentController> logger;

        //constructor
        public StudentController(AppDbContext db, ILogger<StudentController> logger)
        {
            //remember the database
            this.db = db;
            this.logger = logger;
        }

        // GET: api/Student
        //get the list of exams for a user
        //this should always work, as students will need this for finding their exam
        //note that we don't return the Student Number in this call, as this is used to verify the accessor identity
        [HttpGet("Course/{courseId}")]
        public async Task<ActionResult<IEnumerable<Student>>> Get(int courseId)
        {
            ActionResult<IEnumerable<Student>> ar;

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);

                //if anyone asks, then get student names
                //if the owner asks, they get the full set of details
                ar = userId > 0 && await doesOwnCourse(courseId, userId)
                    ? await db.Students.Where(s => s.CourseId == courseId).ToListAsync()
                    : await db.Students.Where(s => s.CourseId == courseId)
                        .Select(s => new Student {Id = s.Id, Name = s.Name}).ToListAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "");
                ar = StatusCode(statusCode: 500);
            }

            return ar;
        }

        // POST api/Student
        [HttpPost]
        public async Task<IdResponse> Post([FromBody] Student student)
        {
            var resp = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    //make sure this user owns the question they want to attach a document to
                    var ownsCourse = await doesOwnCourse(student.CourseId, userId);

                    if (ownsCourse && !string.IsNullOrWhiteSpace(student.Name) &&
                        !string.IsNullOrWhiteSpace(student.Number))
                    {
                        // ReSharper disable once MethodHasAsyncOverload
                        db.Students.Add(student);
                        await db.SaveChangesAsync();
                        resp.Id = student.Id;

                        logger.LogTrace($"Added student {student.Name} to course {student.CourseId}");
                    }
                    else
                    {
                        logger.LogWarning($"{userId} {(ownsCourse ? "" : "not")} owner; fields {student}");
                        resp.ResponseCodes.Add(
                            ownsCourse ? ResponseCodes.EmptyStudentFields : ResponseCodes.InvalidUser);
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
                logger.LogError(ex, $"Failed to add {student.Name} to course {student.CourseId}");
                resp.ResponseCodes.Add(ResponseCodes.InternalError);
            }

            return resp;
        }

        // PUT api/Student/5
        [HttpPut("{id}")]
        public async Task<IdResponse> Put(int id, [FromBody] Student newStudent)
        {
            var resp = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    var student = await db.Students.FirstOrDefaultAsync(s => s.Id == id);
                    if (student != null)
                    {
                        //make sure this user owns the question they want to attach a document to
                        var ownsCourse = await doesOwnCourse(student.CourseId, userId);

                        if (ownsCourse && !string.IsNullOrWhiteSpace(newStudent.Name) &&
                            !string.IsNullOrWhiteSpace(newStudent.Number))
                        {
                            student.Name = newStudent.Name;
                            student.Number = newStudent.Number;
                            student.Email = newStudent.Email;
                            await db.SaveChangesAsync();

                            resp.Id = student.Id;

                            logger.LogTrace($"updated {id} with name {student.Name} number {student.Number}");
                        }
                        else
                        {
                            logger.LogWarning($"{userId} {(ownsCourse ? "" : "not")} owner or invalid data {student}");
                            resp.ResponseCodes.Add(
                                ownsCourse ? ResponseCodes.EmptyStudentFields : ResponseCodes.InvalidUser);
                        }
                    }
                    else
                    {
                        logger.LogWarning($"student {id} not found");
                        resp.ResponseCodes.Add(ResponseCodes.EmptyStudentFields);
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

        // DELETE api/Student/5
        [HttpDelete("{id}")]
        public async Task<IdResponse> Delete(int id)
        {
            var resp = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    var student = await db.Students.FirstOrDefaultAsync(s => s.Id == id);
                    if (student != null)
                    {
                        //make sure this user owns the student they want to delete
                        if (await doesOwnCourse(student.CourseId, userId))
                        {
                            db.Students.Remove(student);
                            await db.SaveChangesAsync();
                            resp.Id = student.Id;

                            logger.LogTrace($"deleted {id}");
                        }
                        else
                        {
                            logger.LogWarning($"{userId} not owner of student {id}");
                            resp.ResponseCodes.Add(ResponseCodes.InvalidUser);
                        }
                    }
                    else
                    {
                        resp.Id = 1;
                        logger.LogWarning($"student {id} not found");
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
                    resp.ResponseCodes.Add(ResponseCodes.StudentInUse);
                    logger.LogWarning("Attempt to delete record in use");
                }
                else
                {
                    logger.LogError(ex, $"failed to delete {id}");
                    resp.ResponseCodes.Add(ResponseCodes.InternalError);
                }
            }

            return resp;
        }

        [HttpPost("Import/{courseId}")]
        public async Task<IdResponse> Import(IFormFile file, int courseId)
        {
            var ir = new IdResponse {Id = 0};

            try
            {
                await using var stream = new MemoryStream();

                if (file != null)
                {
                    await file.CopyToAsync(stream);
                    stream.Seek(offset: 0, SeekOrigin.Begin);

                    using var sr = new StreamReader(stream);
                    var students = await db.Students.Where(s => s.CourseId == courseId).ToListAsync();

                    while (!sr.EndOfStream)
                    {
                        var line = await sr.ReadLineAsync();
                        if (line != null)
                        {
                            var columns = line.Split(new[] {',', '"'}, StringSplitOptions.RemoveEmptyEntries);
                            var number = columns[0].Trim();
                            var name = $"{columns[2].Trim()} {columns[1].Trim()}";
                            if (number.Length > 0 && name.Trim().Length > 0)
                            {
                                var student = new Student {Number = number, Name = name, CourseId = courseId};

                                var oldStudent = students.FirstOrDefault(s => s.Number == student.Number);
                                if (oldStudent == null)
                                    // ReSharper disable once MethodHasAsyncOverload
                                    db.Students.Add(student);
                                else
                                {
                                    oldStudent.Email = student.Email;
                                    oldStudent.Name = student.Name;
                                }
                            }
                        }
                    }

                    await db.SaveChangesAsync();

                    ir.Id = 1;
                }
                else
                    ir.ResponseCodes.Add(ResponseCodes.EmptyStudentFields);
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
                ir.ResponseCodes.Add(ResponseCodes.InternalError);
            }

            return ir;
        }

        private async Task<bool> doesOwnCourse(int courseId, int userId) =>
            await db.Courses.AnyAsync(c => c.Id == courseId && c.UserId == userId);
    }
}