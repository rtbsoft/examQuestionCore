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
    public class CourseController : ControllerBase
    {
        //remember access to the database
        private readonly AppDbContext db;
        private readonly ILogger<CourseController> logger;

        //constructor
        public CourseController(AppDbContext db, ILogger<CourseController> logger)
        {
            //remember the database
            this.db = db;
            this.logger = logger;
        }

        // GET: api/Course
        // return the list of courses for the logged in user
        // these are the ones that can be edited
        [HttpGet]
        public async Task<IEnumerable<Course>> Get()
        {
            IEnumerable<Course> courses = Array.Empty<Course>();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                    courses = await db.Courses.Where(c => c.UserId == userId).ToListAsync();
                logger.LogTrace($"Found {courses.Count()} courses for user {userId}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "");
            }

            return courses;
        }

        // GET: api/Course
        // return the list of courses for the specified user
        // these are generally only to be viewed
        [HttpGet("user/{id}")]
        public async Task<IEnumerable<Course>> GetForUser(int id)
        {
            IEnumerable<Course> courses = Array.Empty<Course>();

            try
            {
                if (await db.Courses.AnyAsync(c => c.UserId == id))
                    courses = await db.Courses.Where(c =>
                            c.UserId == id && db.Exams.Any(e =>
                                e.CourseId == c.Id && e.Start < DateTime.UtcNow.AddMinutes(e.DurationMinutes)))
                        .ToListAsync();

                logger.LogTrace($"Found {courses.Count()} courses for user {id}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"problem retrieving courses for user {id}");
            }

            return courses;
        }

        // POST api/Course
        [HttpPost]
        public async Task<IdResponse> Post([FromBody] Course course)
        {
            var resp = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    if (!string.IsNullOrWhiteSpace(course.Name) && course.Year >= DateTime.UtcNow.Year)
                    {
                        course.UserId = userId;
                        // ReSharper disable once MethodHasAsyncOverload
                        db.Courses.Add(course);
                        await db.SaveChangesAsync();
                        resp.Id = course.Id;

                        logger.LogTrace($"Added course {course.Id} : {course.Name}");
                    }
                    else
                    {
                        logger.LogWarning($"Bad name {course.Name} or {course.Year}");
                        resp.ResponseCodes.Add(ResponseCodes.InvalidCourseFields);
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
                logger.LogError(ex, $"Could not add course {course.Name}");
            }

            return resp;
        }

        // PUT api/Course/5
        [HttpPut("{id}")]
        public async Task<IdResponse> Put(int id, [FromBody] Course newCourse)
        {
            var resp = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
                    if (course != null && !string.IsNullOrWhiteSpace(newCourse.Name) &&
                        newCourse.Year >= DateTime.UtcNow.Year)
                    {
                        course.Name = newCourse.Name;
                        course.Semester = newCourse.Semester;
                        course.Year = newCourse.Year;

                        await db.SaveChangesAsync();
                        resp.Id = course.Id;

                        logger.LogTrace($"Updated course {course.Id} : {course.Name}");
                    }
                    else
                    {
                        logger.LogWarning(
                            $"Could not find {id} or year: {newCourse.Year} and/or name: {newCourse.Name} invalid");
                        if (string.IsNullOrWhiteSpace(newCourse.Name))
                            resp.ResponseCodes.Add(ResponseCodes.InvalidCourseName);
                        if (newCourse.Year < DateTime.UtcNow.Year)
                            resp.ResponseCodes.Add(ResponseCodes.NoEditOldCourse);
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
                logger.LogError(ex, $"Could not update {id}");
            }

            return resp;
        }

        // DELETE api/Course/5
        [HttpDelete("{id}")]
        public async Task<IdResponse> Delete(int id)
        {
            var resp = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
                    if (course != null)
                    {
                        db.Courses.Remove(course);
                        await db.SaveChangesAsync();
                        logger.LogTrace($"deleted {course.Id}");
                    }
                    else
                        logger.LogWarning("{id} not found");

                    resp.Id = id;
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
                    resp.ResponseCodes.Add(ResponseCodes.CourseInUse);
                    logger.LogWarning("Attempt to delete record in use");
                }
                else
                    logger.LogError(ex, $"failed to delete {id}");
            }

            return resp;
        }
    }
}