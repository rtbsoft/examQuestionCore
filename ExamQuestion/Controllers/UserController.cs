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
        public async Task<IEnumerable<User>> Get(int schoolId) =>
            await db.Users.Where(u => u.SchoolId == schoolId).Select(u => new User {Id = u.Id, Name = u.Name})
                .ToListAsync();
    }
}