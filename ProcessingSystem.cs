using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace IndustrialProcessing
{
    public class ProcessingSystem
    {
        private readonly int _maxQueueSize;
        private readonly List<Job> _jobQueue = new List<Job>();
        private readonly HashSet<Guid> _processedJobs = new HashSet<Guid>(); // Idempotentnost
        private readonly object _lockObj = new object();
        private readonly List<JobStats> _stats = new List<JobStats>();

        public event Action<Guid, int> JobCompleted;
        public event Action<Guid, string> JobFailed;

        public ProcessingSystem(int maxQueueSize, int workerCount)
        {
            _maxQueueSize = maxQueueSize;

            for (int i = 0; i < workerCount; i++)
            {
                Thread worker = new Thread(WorkerLoop) { IsBackground = true };
                worker.Start();
            }


            Timer reportTimer = new Timer(GenerateReport, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
        }

        public JobHandle Submit(Job job)
        {
            lock (_lockObj)
            {
                if (_jobQueue.Count >= _maxQueueSize)
                {
                    throw new InvalidOperationException("Red je pun (MaxQueueSize dostignut).");
                }

                if (_processedJobs.Contains(job.Id))
                {
                    throw new ArgumentException("Idempotentnost prekršena: Posao sa ovim ID-jem je već obrađen ili je u redu.");
                }

                _processedJobs.Add(job.Id);
                _jobQueue.Add(job);

                // Sortiramo po prioritetu (manji broj = veći prioritet)
                _jobQueue.Sort((x, y) => x.Priority.CompareTo(y.Priority));

                Monitor.PulseAll(_lockObj);
            }


            var tcs = new TaskCompletionSource<int>();

            Action<Guid, int> onCompleted = null;
            Action<Guid, string> onFailed = null;

            onCompleted = (id, res) => { if (id == job.Id) { tcs.TrySetResult(res); Cleanup(); } };
            onFailed = (id, err) => { if (id == job.Id) { tcs.TrySetException(new Exception(err)); Cleanup(); } };

            JobCompleted += onCompleted;
            JobFailed += onFailed;

            void Cleanup()
            {
                JobCompleted -= onCompleted;
                JobFailed -= onFailed;
            }

            return new JobHandle { Id = job.Id, Result = tcs.Task };
        }

        private void WorkerLoop()
        {
            while (true)
            {
                Job currentJob = null;

                lock (_lockObj)
                {
                    while (_jobQueue.Count == 0)
                    {
                        Monitor.Wait(_lockObj); // Ceka dok se ne doda novi posao
                    }

                    currentJob = _jobQueue[0];
                    _jobQueue.RemoveAt(0);
                    Monitor.PulseAll(_lockObj);
                }


                _ = ProcessJobWithRetryAsync(currentJob);
            }
        }

        private async Task ProcessJobWithRetryAsync(Job job)
        {
            int attempts = 0;
            bool success = false;
            int finalResult = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();

            while (attempts < 3 && !success)
            {
                attempts++;
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)); // 2 sekunde timeout
                    finalResult = await ExecuteJobLogicAsync(job, cts.Token);
                    success = true;
                }
                catch (OperationCanceledException)
                {
                    // Timeout
                }
                catch (Exception)
                {
                    // Neka druga greška
                }
            }

            watch.Stop();

            lock (_lockObj)
            {
                _stats.Add(new JobStats { JobId = job.Id, Type = job.Type, IsSuccessful = success, ExecutionTimeMs = watch.ElapsedMilliseconds });
            }

            if (success)
            {
                JobCompleted?.Invoke(job.Id, finalResult);
                await Logger.LogAsync("COMPLETED", job.Id, finalResult.ToString());
            }
            else
            {
                JobFailed?.Invoke(job.Id, "ABORT");
                await Logger.LogAsync("ABORT", job.Id, "Job Failed after 3 attempts");
            }
        }

        private async Task<int> ExecuteJobLogicAsync(Job job, CancellationToken token)
        {
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                if (job.Type == JobType.Prime)
                {
                    var parts = job.Payload.Split(',');
                    int maxNumber = int.Parse(parts[0].Split(':')[1]);
                    int threads = int.Parse(parts[1].Split(':')[1]);
                    threads = Math.Clamp(threads, 1, 8); // Ograničenje [1, 8]

                    int primeCount = 0;
                    object countLock = new object();

                    Parallel.For(2, maxNumber + 1, new ParallelOptions { MaxDegreeOfParallelism = threads, CancellationToken = token }, i =>
                    {
                        if (IsPrime(i))
                        {
                            lock (countLock) { primeCount++; }
                        }
                    });

                    return primeCount;
                }
                else if (job.Type == JobType.IO)
                {
                    int delay = int.Parse(job.Payload.Split(':')[1]);
                    Thread.Sleep(delay); // Simulacija blokirajućeg I/O
                    token.ThrowIfCancellationRequested();

                    return new Random().Next(0, 101); // Random broj 0-100
                }

                throw new NotSupportedException("Nepoznat tip posla.");
            }, token);
        }

        private bool IsPrime(int number)
        {
            if (number < 2) return false;
            for (int i = 2; i <= Math.Sqrt(number); i++)
                if (number % i == 0) return false;
            return true;
        }

        public List<Job> GetTopJobs(int n)
        {
            lock (_lockObj)
            {
                return _jobQueue.Take(n).ToList();
            }
        }

        public Job GetJob(Guid id)
        {
            lock (_lockObj)
            {
                return _jobQueue.FirstOrDefault(j => j.Id == id);
            }
        }

        private int _reportIndex = 0;

        private void GenerateReport(object state)
        {
            List<JobStats> snapshot;
            lock (_lockObj)
            {
                snapshot = new List<JobStats>(_stats);
            }

            var reportData = snapshot
                .GroupBy(s => s.Type)
                .Select(g => new
                {
                    Type = g.Key,
                    CompletedCount = g.Count(x => x.IsSuccessful),
                    AvgTime = g.Where(x => x.IsSuccessful).Select(x => x.ExecutionTimeMs).DefaultIfEmpty(0).Average(),
                    FailedCount = g.Count(x => !x.IsSuccessful)
                }).ToList();

            XElement root = new XElement("Report",
                reportData.Select(r => new XElement("JobStat",
                    new XAttribute("Type", r.Type),
                    new XElement("Completed", r.CompletedCount),
                    new XElement("AvgTimeMs", r.AvgTime),
                    new XElement("Failed", r.FailedCount)
                ))
            );

            string fileName = $"Report_{_reportIndex}.xml";
            root.Save(fileName);

            _reportIndex = (_reportIndex + 1) % 10;

        }
    }
}