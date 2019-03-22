using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace FolderCleanerService
{
    public class Utils
    {
        public static string CalculateFileSHA256(string filePath)
        {
            byte[] hash;

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    hash = SHA256.Create().ComputeHash(fs);
                }
            }
            catch (Exception) { return (null); }

            var sb = new StringBuilder();

            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }

            return (sb.ToString());
        }
    }
}
