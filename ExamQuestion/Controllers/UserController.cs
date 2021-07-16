using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ExamQuestion.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExamQuestion.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext db;
        private readonly ILogger<UserController> logger;

        public UserController(AppDbContext db, ILogger<UserController> logger)
        {
            //remember the database
            this.db = db;
            this.logger = logger;
        }

        // GET: api/<UserController>
        [HttpGet("{schoolId}")]
        public async Task<ActionResult<IEnumerable<User>>> Get(int schoolId)
        {
            ActionResult<IEnumerable<User>> ar;

            try
            {
                //if anyone asks, then get student names
                //if the owner asks, they get the full set of details
                ar = await db.Users.Where(u => u.SchoolId == schoolId).Select(u => new User {Id = u.Id, Name = u.Name})
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "");
                ar = StatusCode(statusCode: 500);
            }

            return ar;
        }

    }
}