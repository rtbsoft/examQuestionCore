using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ExamQuestion.Utils
{
    public static class Util
    {
        public static async Task<int> GetLoggedInUser(HttpContext context)
        {
            await context.Session.LoadAsync();
            var userId = context.Session.GetInt32("UserId");

            return userId ?? 0;
        }

        public static string GetDocumentPath() => "wwwroot\\data";

        public static bool IsValidEmailAddress(this string address) =>
            !(address is null) && new EmailAddressAttribute().IsValid(address);
    }
}