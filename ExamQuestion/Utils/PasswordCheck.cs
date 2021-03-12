using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ExamQuestion.Utils
{
    public class PasswordCheck
    {
        private const string url = "https://api.pwnedpasswords.com/range/";

        internal static async Task<bool> IsStrong(string password)
        {
            //hash the password given with SHA-1
            var hashBytes = SHA1.HashData(Encoding.ASCII.GetBytes(password.ToCharArray()));
            var hash = string.Join(string.Empty, Array.ConvertAll(hashBytes, b => b.ToString("X2")));
            var hashPrefix = hash.Substring(0, 5);
            var hashSuffix = hash.Substring(5);

            //send the first 5 characters of the result
            var hashes = await getAsync($"{url}{hashPrefix}");

            //if the hash is found in the returned list, password is bad
            return !hashes.Contains(hashSuffix);
        }

        private static async Task<string> getAsync(string uri)
        {
            var request = (HttpWebRequest)WebRequest.Create(uri);
            using var response = (HttpWebResponse)await request.GetResponseAsync();
            await using var stream = response.GetResponseStream();
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
    }
}