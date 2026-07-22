using System;
using System.Security.Cryptography;
using System.Text;

namespace TavernLib.Backend
{
    public static class BackendUtils
    {
        public static string TavernApi => "http://themoddingtavern.com:1763";
        public static string ServerUri => "/servers";


        public static string HashDigest(string input)
        {
            using var hash = SHA256.Create();
            var byteArray = hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(byteArray).Replace("-", "").ToLower();
        }
        
        public static string TokenUrlSafe(int nbytes = 32)
        {
            var bytes = new byte[nbytes];
            using (var rng = RandomNumberGenerator.Create()) { rng.GetBytes(bytes); }

            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}