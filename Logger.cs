using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace IndustrialProcessing
{
    public static class Logger
    {
        private static readonly string _logFilePath = "Log.txt";

        // SemaphoreSlim osigurava da samo jedna nit u jednom trenutku pristupa fajlu (zamena za lock u asinhronom svetu)
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public static async Task LogAsync(string status, Guid jobId, string result)
        {
            // Formatiranje tačno po zahtevu zadatka
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{status}] {jobId}, {result}";

            // Čekamo da fajl bude slobodan za upis
            await _semaphore.WaitAsync();
            try
            {
                // true = append (dodajemo na kraj fajla umesto da ga prepisujemo)
                using (StreamWriter sw = new StreamWriter(_logFilePath, true))
                {
                    await sw.WriteLineAsync(logMessage);
                }
            }
            finally
            {
                // Oslobađamo fajl za druge niti
                _semaphore.Release();
            }
        }
    }
}