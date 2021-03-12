using System;
using System.ComponentModel.DataAnnotations;
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
    //specify the route for code in this controller
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        //remember access to the database
        private readonly AppDbContext db;
        private readonly ILogger<AccountController> logger;

        //constructor
        public AccountController(AppDbContext appDbContext, ILogger<AccountController> logger)
        {
            //remember the database
            db = appDbContext;
            this.logger = logger;
        }

        [HttpGet("User")]
        public async Task<User> GetUser()
        {
            await HttpContext.Session.LoadAsync();
            var userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            User matchingUser = null;

            if (userId > 0)
                matchingUser = await db.Users.Select(u =>
                        new User {Id = u.Id, Name = u.Name, Email = u.Email, SchoolId = u.SchoolId})
                    .FirstOrDefaultAsync(u => u.Id == userId);

            return matchingUser;
        }

        [HttpPut("User/{id}")]
        public async Task<IdResponse> UpdateUser(int id, [FromBody] User user)
        {
            var ps = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
                    if (existingUser != null && userId == existingUser.Id)
                    {
                        var isValidEmail = !string.IsNullOrWhiteSpace(user.Email) &&
                                           new EmailAddressAttribute().IsValid(user.Email) &&
                                           !db.Users.Any(u => u.Email == user.Email && u.Id != id);
                        var isValidName = !string.IsNullOrWhiteSpace(user.Email);
                        var isValidSchool = db.Schools.Any(s => s.Id == user.SchoolId);
                        //check for valid fields
                        if (isValidEmail && isValidName && isValidSchool)
                        {
                            //save the user
                            existingUser.Email = user.Email;
                            existingUser.SchoolId = user.SchoolId;
                            existingUser.Name = user.Name;
                            await db.SaveChangesAsync();

                            //let the client know that it was done successfully by returning the Id
                            ps.Id = existingUser.Id;

                            logger.LogTrace($"Updated user {existingUser}");
                        }
                        else
                        {
                            logger.LogWarning($"invalid or missing info: {user}");
                            if (!isValidEmail)
                                ps.ResponseCodes.Add(ResponseCodes.InvalidEmail);
                            if (!isValidName)
                                ps.ResponseCodes.Add(ResponseCodes.InvalidName);
                            if (!isValidSchool)
                                ps.ResponseCodes.Add(ResponseCodes.InvalidSchool);
                        }
                    }
                    else
                    {
                        logger.LogWarning($"user {id} does not exist or attempt to modify another user by {userId}");
                        ps.ResponseCodes.Add(ResponseCodes.InvalidUser);
                    }
                }
                else
                {
                    logger.LogWarning("Attempt without logging in");
                    ps.ResponseCodes.Add(ResponseCodes.NotLoggedIn);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"failed to update user {user}");
            }

            return ps;
        }

        [HttpPut("Password/{id}")]
        public async Task<IdResponse> UpdatePassword(int id, [FromBody] PasswordRequest request)
        {
            var ps = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    var matchingUser = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
                    if (matchingUser != null && matchingUser.Id == userId)
                    {
                        if (request.OldPassword != null &&
                            PasswordHash.VerifyHashedPassword(matchingUser.Password, request.OldPassword))
                        {
                            if (await PasswordCheck.IsStrong(request.NewPassword))
                            {
                                matchingUser.Password = PasswordHash.HashPassword(request.NewPassword);
                                await db.SaveChangesAsync();
                                ps.Id = id;

                                logger.LogTrace($"Changed password for user {id}");
                            }
                            else
                            {
                                logger.LogWarning($"attempt to use weak password {request.NewPassword}");
                                ps.ResponseCodes.Add(ResponseCodes.WeakPassword);
                            }
                        }
                        else
                        {
                            logger.LogWarning($"incorrect password for user {id}");
                            ps.ResponseCodes.Add(ResponseCodes.InvalidPassword);
                        }
                    }
                    else
                    {
                        logger.LogWarning($"user {id} does not exist or attempt to modify another user's password by {userId}");
                        ps.ResponseCodes.Add(ResponseCodes.InvalidUser);
                    }
                }
                else
                {
                    logger.LogWarning("Attempt without logging in");
                    ps.ResponseCodes.Add(ResponseCodes.NotLoggedIn);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"failed to update password for {id}");
            }

            return ps;
        }

        // POST api/Account/Register
        //FromBody indicates that the data for that object should be found
        //in the body of the message received
        [HttpPost("Register")]
        public async Task<IdResponse> Register([FromBody] User newUser)
        {
            var ps = new IdResponse();

            try
            {
                newUser.Email = newUser.Email?.ToLower();

                //check if there are already users with this email
                if (!db.Users.Any(u => u.Email == newUser.Email))
                    //check for valid email
                {
                    if (!string.IsNullOrWhiteSpace(newUser.Email) &&
                        !string.IsNullOrWhiteSpace(newUser.Password) &&
                        new EmailAddressAttribute().IsValid(newUser.Email))
                    {
                        if (await PasswordCheck.IsStrong(newUser.Password))
                        {
                            //if all good, convert the password into its hash
                            newUser.Password = PasswordHash.HashPassword(newUser.Password);

                            //save the user
                            // ReSharper disable once MethodHasAsyncOverload
                            db.Users.Add(newUser);
                            await db.SaveChangesAsync();

                            //let the client know that it was done successfully by returning the Id
                            ps.Id = newUser.Id;

                            //save the user's ID to the session -- ie. we're logged in
                            await HttpContext.Session.LoadAsync();
                            HttpContext.Session.SetInt32("UserId", newUser.Id);
                            await HttpContext.Session.CommitAsync();

                            logger.LogTrace($"Created user {newUser.Id} for {newUser.Email}");
                        }
                        else
                        {
                            logger.LogWarning($"attempt to use poor password {newUser.Password}");
                            ps.ResponseCodes.Add(ResponseCodes.WeakPassword);
                        }
                    }
                    else
                    {
                        logger.LogWarning($"missing info: {newUser}");
                        ps.ResponseCodes.Add(ResponseCodes.InvalidCredentials);
                    }
                }
                else
                {
                    logger.LogWarning($"email {newUser.Email} already exists");
                    ps.ResponseCodes.Add(ResponseCodes.EmailInUse);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"failed to create user {newUser}");
            }

            return ps;
        }

        // POST api/Account/Login
        //FromBody indicates that the data for that object should be found
        //in the body of the message received
        [HttpPost("Login")]
        public async Task<IdResponse> Login([FromBody] User user)
        {
            var ps = new IdResponse();

            try
            {
                //find the user with this email
                var matchingUser = await db.Users.FirstOrDefaultAsync(u => u.Email == user.Email.ToLower());

                if (matchingUser != null && PasswordHash.VerifyHashedPassword(matchingUser.Password, user.Password))
                {
                    //save the user's ID to the session -- ie. we're logged in
                    await HttpContext.Session.LoadAsync();
                    HttpContext.Session.SetInt32("UserId", matchingUser.Id);
                    await HttpContext.Session.CommitAsync();

                    ps.Id = matchingUser.Id;
                    logger.LogTrace($"User {matchingUser.Id} has logged in");
                }
                else
                {
                    logger.LogWarning($"User {user.Email} failed to login " +
                                      $"{(matchingUser == null ? "no match" : "bad password")}");
                    ps.ResponseCodes.Add(ResponseCodes.IncorrectCredentials);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Could not log in {user.Email}");
            }

            return ps;
        }

        [HttpDelete("Logout")]
        public async Task<IdResponse> Logout()
        {
            var resp = new IdResponse();

            try
            {
                await HttpContext.Session.LoadAsync();
                var userId = HttpContext.Session.GetInt32("userId");
                HttpContext.Session.Remove("UserId");
                await HttpContext.Session.CommitAsync();
                resp.Id = userId ?? 1;

                logger.LogTrace($"User {userId} has logged out");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "failed to logout");
            }

            return resp;
        }

        [HttpDelete("{id}")]
        public async Task<IdResponse> Delete(int id)
        {
            var resp = new IdResponse();

            try
            {
                var userId = await Util.GetLoggedInUser(HttpContext);
                if (userId > 0)
                {
                    if (userId == id)
                    {
                        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
                        db.Users.Remove(user);
                        await db.SaveChangesAsync();

                        resp.Id = id;
                    }
                    else
                    {
                        logger.LogWarning($"{userId} attempted to delete {id}");
                        resp.ResponseCodes.Add(ResponseCodes.DeleteOtherUser);
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
                    resp.ResponseCodes.Add(ResponseCodes.UserInUse);
                    logger.LogWarning("Attempt to delete record in use");
                }
                else
                    logger.LogError(ex, $"failed to delete {id}");
            }

            return resp;
        }
    }
}