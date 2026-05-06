using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace IndustrialProcessing.Tests
{
    public class ProcessingSystemTests
    {
        /// <summary>
        /// TEST 1: Proverava ograničenje kapaciteta reda (MaxQueueSize).
        /// Očekivano ponašanje: Sistem mora da baci InvalidOperationException 
        /// ukoliko pokušamo da dodamo novi posao, a red je već dostigao svoj maksimalni kapacitet.
        /// </summary>
        [Fact]
        public void Submit_ShouldThrowException_WhenQueueIsFull()
        {
            // Arrange
            var system = new ProcessingSystem(maxQueueSize: 1, workerCount: 0); // 0 workera da bi se napunio red
            system.Submit(new Job { Type = JobType.IO, Payload = "delay:100" });

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                system.Submit(new Job { Type = JobType.Prime, Payload = "numbers:10,threads:1" });
            });
        }

        /// <summary>
        /// TEST 2: Proverava mehanizam idempotencije.
        /// Očekivano ponašanje: Sistem ne sme da dozvoli dodavanje posla sa Guid identifikatorom 
        /// koji već postoji u sistemu. Mora baciti ArgumentException.
        /// </summary>
        [Fact]
        public void Submit_Idempotency_ShouldThrowExceptionForSameId()
        {
            // Arrange
            var system = new ProcessingSystem(maxQueueSize: 5, workerCount: 0);
            var job = new Job { Type = JobType.IO, Payload = "delay:100" };

            system.Submit(job);

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
            {
                system.Submit(job); // Isti ID
            });
        }

        /// <summary>
        /// TEST 3: Proverava sortiranje reda čekanja prema prioritetu.
        /// Očekivano ponašanje: Poslovi se moraju sortirati tako da manji broj označava veći prioritet. 
        /// Metoda GetTopJobs mora vratiti poslove po ispravnom redosledu.
        /// </summary>
        [Fact]
        public void GetTopJobs_ShouldReturnOrderedByPriority()
        {
            // Arrange
            var system = new ProcessingSystem(maxQueueSize: 10, workerCount: 0);
            system.Submit(new Job { Priority = 5, Payload = "delay:10" });
            system.Submit(new Job { Priority = 1, Payload = "delay:10" }); // Veci prioritet
            system.Submit(new Job { Priority = 3, Payload = "delay:10" });

            // Act
            // .ToList() da bi radio Count property i indeksiranje [0] i [1]
            var topJobs = system.GetTopJobs(2).ToList();

            // Assert
            Assert.Equal(2, topJobs.Count);
            Assert.Equal(1, topJobs[0].Priority);
            Assert.Equal(3, topJobs[1].Priority);
        }

        /// <summary>
        /// TEST 4: Proverava uspešno izvršavanje Prime posla i okidanje JobCompleted događaja.
        /// Očekivano ponašanje: Radna nit treba da izračuna broj prostih brojeva (za do 10, to je 4), 
        /// da popuni Task rezultat sa tim brojem i uspešno okine JobCompleted event.
        /// </summary>
        [Fact]
        public async Task ProcessJob_ShouldFireJobCompletedEvent_ForValidPrimeJob()
        {
            // Arrange
            var system = new ProcessingSystem(maxQueueSize: 5, workerCount: 1);
            var job = new Job { Type = JobType.Prime, Payload = "numbers:10,threads:1", Priority = 1 };

            bool eventFired = false;
            int finalResult = 0;

            system.JobCompleted += (id, result) =>
            {
                if (id == job.Id)
                {
                    eventFired = true;
                    finalResult = result;
                }
            };

            // Act
            var handle = system.Submit(job);

            // Cekamo da se posao zavrsi (rezultat za proste brojeve do 10 je 4: 2,3,5,7)
            await handle.Result;

            // Assert
            Assert.True(eventFired);
            Assert.Equal(4, finalResult);
        }

        /// <summary>
        /// TEST 5: Proverava mehanizam Timeout-a pomoću CancellationToken-a.
        /// Očekivano ponašanje: Pošto zadatak traje 3 sekunde, a limit je 2 sekunde, 
        /// posao mora da padne i da okine događaj JobFailed.
        /// </summary>
        [Fact]
        public async Task ProcessJob_ShouldFireJobFailedEvent_ForTimeoutIOJob()
        {
            // Arrange
            var system = new ProcessingSystem(maxQueueSize: 5, workerCount: 1);
            // Stavljamo delay 3000ms, a nas timeout u kodu je 2000ms (2 sekunde), tako da mora pasti
            var job = new Job { Type = JobType.IO, Payload = "delay:3000", Priority = 1 };

            bool failedEventFired = false;

            system.JobFailed += (id, error) =>
            {
                if (id == job.Id)
                {
                    failedEventFired = true;
                }
            };

            // Act
            var handle = system.Submit(job);

            try
            {
                await handle.Result;
            }
            catch
            {
                // Ocekujemo da Task baci izuzetak jer je Job propao
            }

            // Dodajemo mali delay da damo vremena eventu da se okine nakon catch bloka
            await Task.Delay(100);

            // Assert
            Assert.True(failedEventFired);
        }

        /// <summary>
        /// TEST 6: Proverava uspešno dohvatanje posla preko njegovog ID-ja.
        /// Očekivano ponašanje: Metoda GetJob mora vratiti tačnu referencu na instancu posla 
        /// ukoliko taj posao postoji u redu čekanja.
        /// </summary>
        [Fact]
        public void GetJob_ShouldReturnCorrectJob_WhenJobExists()
        {
            // Arrange
            var system = new ProcessingSystem(maxQueueSize: 5, workerCount: 0);
            var job = new Job { Type = JobType.IO, Payload = "delay:100" };
            system.Submit(job);

            // Act
            var retrievedJob = system.GetJob(job.Id);

            // Assert
            Assert.NotNull(retrievedJob);
            Assert.Equal(job.Id, retrievedJob.Id);
        }

        /// <summary>
        /// TEST 7: Proverava ponašanje prilikom pretrage nepostojećeg posla.
        /// Očekivano ponašanje: Metoda GetJob treba da vrati null, 
        /// a ne da pukne ukoliko posao sa datim Guid-om ne postoji.
        /// </summary>
        [Fact]
        public void GetJob_ShouldReturnNull_WhenJobDoesNotExist()
        {
            // Arrange
            var system = new ProcessingSystem(maxQueueSize: 5, workerCount: 0);

            // Act
            var retrievedJob = system.GetJob(Guid.NewGuid());

            // Assert
            Assert.Null(retrievedJob);
        }

        /// <summary>
        /// TEST 8: Proverava zaštitu od loše formiranog Payload-a i robusnost radne niti.
        /// Očekivano ponašanje: Parsiranje nevalidnog stringa baciće Exception. Sistem to mora uhvatiti 
        /// (Try-Catch blok), označiti posao kao neuspešan i okinuti JobFailed događaj, bez rušenja celog programa.
        /// </summary>
        [Fact]
        public async Task ProcessJob_ShouldFireFailedEvent_WhenPayloadIsInvalid()
        {
            // Arrange
            var system = new ProcessingSystem(maxQueueSize: 5, workerCount: 1);
            // Namerno saljemo los format teksta da bismo testirali Catch blokove (podize coverage)
            var job = new Job { Type = JobType.Prime, Payload = "NEVALIDAN_TEKST", Priority = 1 };

            bool failedEventFired = false;
            system.JobFailed += (id, error) =>
            {
                if (id == job.Id) failedEventFired = true;
            };

            // Act
            var handle = system.Submit(job);
            try
            {
                await handle.Result;
            }
            catch
            {
                // Ocekujemo Exception jer payload ne moze da se parsira
            }

            await Task.Delay(100); // Dajemo malo vremena asinhronom eventu da se okine

            // Assert
            Assert.True(failedEventFired);
        }

        /// <summary>
        /// TEST 9: Proverava uspešno izvršavanje IO posla.
        /// Očekivano ponašanje: IO posao sa kratkim delay-om (npr. 100ms) mora da prođe bez timeout-a
        /// i da vrati generisan nasumični broj između 0 i 100 (kako je traženo u zadatku).
        /// </summary>
        [Fact]
        public async Task ProcessJob_ShouldReturnRandomNumber_ForValidIOJob()
        {
            // Arrange
            var system = new ProcessingSystem(maxQueueSize: 5, workerCount: 1);
            var job = new Job { Type = JobType.IO, Payload = "delay:100", Priority = 1 };

            bool eventFired = false;
            int finalResult = -1;

            system.JobCompleted += (id, result) =>
            {
                if (id == job.Id)
                {
                    eventFired = true;
                    finalResult = result;
                }
            };

            // Act
            var handle = system.Submit(job);
            await handle.Result; // Cekamo da se posao zavrsi

            // Assert
            Assert.True(eventFired);
            Assert.True(finalResult >= 0 && finalResult <= 100, $"Rezultat {finalResult} nije u opsegu [0, 100].");
        }

        /// <summary>
        /// TEST 10: Proverava zaštitu sistema od nepoznatog tipa posla.
        /// Očekivano ponašanje: Ako se na silu prosledi JobType koji sistem ne prepoznaje,
        /// mora se baciti NotSupportedException (koji se hvata u try-catch) i okida se JobFailed događaj.
        /// </summary>
        [Fact]
        public async Task ProcessJob_ShouldFail_ForUnknownJobType()
        {
            // Arrange
            var system = new ProcessingSystem(maxQueueSize: 5, workerCount: 1);
            // Kastujemo nasumičan broj u JobType kako bismo simulirali nepoznatu vrednost (npr. vrednost 99)
            var job = new Job { Type = (JobType)99, Payload = "nebitno", Priority = 1 };

            bool failedEventFired = false;

            system.JobFailed += (id, error) =>
            {
                if (id == job.Id) failedEventFired = true;
            };

            // Act
            var handle = system.Submit(job);
            try
            {
                await handle.Result;
            }
            catch
            {
                // Ocekujemo da pukne jer tip 99 ne postoji
            }

            // Assert
            Assert.True(failedEventFired);
        }
    }
}