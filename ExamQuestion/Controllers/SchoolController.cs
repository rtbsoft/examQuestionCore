using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ExamQuestion.Models;
using ExamQuestion.Utils;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace ExamQuestion.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SchoolController : ControllerBase
    {
        private readonly AppDbContext db;
        private readonly ILogger<SchoolController> logger;

        public SchoolController(AppDbContext db, ILogger<SchoolController> logger)
        {
            //remember the database
            this.db = db;
            this.logger = logger;
        }

        // GET: api/<SchoolController>
        // always return the list of schools
        [HttpGet]
        public async Task<ActionResult<IEnumerable<School>>> Get()
        {
            ActionResult<IEnumerable<School>> ar;

            try
            {
                ar = await db.Schools.ToListAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "");
                ar = StatusCode(statusCode: 500);
            }

            return ar;
        }

        // POST api/<SchoolController>
        [HttpPost]
        public async Task<IdResponse> Post([FromBody] School school)
        {
            var resp = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    if (!string.IsNullOrWhiteSpace(school.Name))
                    {
                        // ReSharper disable once MethodHasAsyncOverload
                        db.Schools.Add(school);
                        await db.SaveChangesAsync();
                        resp.Id = school.Id;

                        logger.LogTrace($"Added school {school.Name}");
                    }
                    else
                    {
                        logger.LogWarning("Invalid school name");
                        resp.ResponseCodes.Add(ResponseCodes.InvalidSchoolFields);
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
                logger.LogError(ex, $"Failed to add {school.Name}");
                resp.ResponseCodes.Add(ResponseCodes.InternalError);
            }

            return resp;
        }

        // PUT api/<SchoolController>/5
        [HttpPut("{id}")]
        public async Task<IdResponse> Put(int id, [FromBody] School newSchool)
        {
            var resp = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    var school = await db.Schools.FirstOrDefaultAsync(s => s.Id == id);
                    if (school != null)
                    {
                        if (!string.IsNullOrWhiteSpace(newSchool.Name))
                        {
                            school.Name = newSchool.Name;
                            await db.SaveChangesAsync();

                            resp.Id = school.Id;

                            logger.LogTrace($"updated {id} with name {school.Name}");
                        }
                        else
                        {
                            logger.LogWarning($"invalid data {school}");
                            resp.ResponseCodes.Add(ResponseCodes.InvalidSchoolFields);
                        }
                    }
                    else
                    {
                        logger.LogWarning($"school {id} not found");
                        resp.ResponseCodes.Add(ResponseCodes.InvalidSchoolFields);
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

        // DELETE api/<SchoolController>/5
        [HttpDelete("{id}")]
        public async Task<IdResponse> Delete(int id)
        {
            var resp = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    var school = await db.Schools.FirstOrDefaultAsync(s => s.Id == id);
                    if (school != null)
                    {
                        db.Schools.Remove(school);
                        await db.SaveChangesAsync();
                        resp.Id = school.Id;

                        logger.LogTrace($"deleted {id}");
                    }
                    else
                    {
                        resp.Id = 1;
                        logger.LogWarning($"school {id} not found");
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
                    resp.ResponseCodes.Add(ResponseCodes.SchoolInUse);
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
    }
}