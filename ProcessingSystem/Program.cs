using System;
using System.Threading;
using System.Xml.Linq;

namespace IndustrialProcessing
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // Učitavanje konfiguracije
                XDocument config = XDocument.Load("SystemConfig.xml");
                int workerCount = int.Parse(config.Root.Element("WorkerCount").Value);
                int maxQueueSize = int.Parse(config.Root.Element("MaxQueueSize").Value);

                var processingSystem = new ProcessingSystem(maxQueueSize, workerCount);

                // Pretplata na evente pomoću lambda izraza
                processingSystem.JobCompleted += (id, result) => Console.WriteLine($"[Event] Job {id} Completed. Result: {result}");
                processingSystem.JobFailed += (id, error) => Console.WriteLine($"[Event] Job {id} Failed. Error: {error}");

                // Učitavanje inicijalnih poslova
                foreach (var jobElement in config.Root.Element("Jobs").Elements("Job"))
                {
                    JobType type = Enum.Parse<JobType>(jobElement.Attribute("Type").Value);
                    string payload = jobElement.Attribute("Payload").Value;
                    int priority = int.Parse(jobElement.Attribute("Priority").Value);

                    var job = new Job { Type = type, Payload = payload, Priority = priority };
                    processingSystem.Submit(job);
                }

                // Pokretanje Producer niti
                for (int i = 0; i < workerCount; i++)
                {
                    Thread producer = new Thread(() =>
                    {
                        Random rnd = new Random();
                        while (true)
                        {
                            try
                            {
                                var newJob = new Job
                                {
                                    Type = rnd.Next(2) == 0 ? JobType.Prime : JobType.IO,
                                    Payload = "delay:" + rnd.Next(500, 3000), // Za testiranje
                                    Priority = rnd.Next(1, 5)
                                };
                                processingSystem.Submit(newJob);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[Producer Error]: {ex.Message}");
                            }
                            Thread.Sleep(rnd.Next(500, 2000));
                        }
                    })
                    { IsBackground = true };
                    producer.Start();
                }

                Console.WriteLine("Sistem je pokrenut. Pritisni ENTER za prekid.");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Kritična greška u Main: {ex.Message}");
            }
        }
    }
}