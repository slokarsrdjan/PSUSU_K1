using System;
using System.IO;
using System.Threading.Tasks;

namespace IndustrialProcessing
{
    public static class Logger
    {
        private static readonly string logFilePath = "processing_log.txt";
        private static readonly object _lock = new object();

        public static async Task LogAsync(string status, Guid jobId, string result)
        {
            string message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ({status}) {jobId}, {result}\n";

            // Koristimo lokalni lock za file I/O thread-safety 
            byte[] encodedText = System.Text.Encoding.UTF8.GetBytes(message);

            using (FileStream sourceStream = new FileStream(logFilePath,
                FileMode.Append, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: true))
            {
                await sourceStream.WriteAsync(encodedText, 0, encodedText.Length);
            }
        }
    }
}