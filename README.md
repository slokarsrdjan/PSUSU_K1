# Industrial Processing System API

Ovaj projekat predstavlja **thread-safe** C# servis (`ProcessingSystem`) dizajniran za asinhronu i konkurentnu obradu industrijskih poslova (Jobs). Sistem je zasnovan na *Producer-Consumer* arhitekturi i demonstrira napredne koncepte višenitnog (multithreaded) programiranja, uključujući sinhronizaciju, prioritete i event-driven arhitekturu.

## 🚀 Ključne funkcionalnosti

* **Asinhrona i konkurentna obrada**: Sistem koristi `Task` i višenitnost za asinhrono izvršavanje poslova bez blokiranja glavne niti.
* **Redosled po prioritetima**: Integrisan je mehanizam prioriteta (poslovi sa manjim brojem imaju viši prioritet) putem `PriorityQueue` strukture.
* **Idempotentnost**: Ugrađene su provere koje garantuju da se posao sa istim identifikatorom (`Guid`) neće izvršiti više puta.
* **Event-Driven pristup**: Sistem okida događaje za ključne tačke (uspešno završen posao, greška pri obradi, itd.).
* **Ograničenje resursa (MaxQueueSize)**: Ograničen kapacitet reda za čekanje kako bi se sprečilo preopterećenje sistema (novi poslovi se odbijaju ako je red pun).
* **XML Konfiguracija**: Inicijalizacija sistema, broja *worker* niti i početnih poslova vrši se dinamički iz XML konfiguracionog fajla.
* **Logovanje i izveštaji**: Generisanje do 10 XML izveštaja (novi prepisuju najstarije) i upis logova u obično tekstualno okruženje (`Log.txt`).

## 🛠️ Tipovi poslova (Jobs)

Sistem podržava dve glavne vrste zadataka (`JobType`):

1.  **Prime (Prosti brojevi)**
    * Izračunava broj prostih brojeva do zadate vrednosti unutar payload-a.
    * Posao se obavlja paralelno.
    * Broj niti za svaki pojedinačni `Prime` posao je limitiran opsegom [1, 8].
2.  **IO (Ulaz/Izlaz)**
    * Simulira asinhrono čitanje vrednosti sa zadate adrese.
    * Ukoliko čitanje potraje duže od definisanog *timeout*-a, posao se prekida i beleži se greška.
    * Uspesan rezultat je nasumičan broj od 0 do 100.

## 📁 Struktura projekta

* **`Models/`**
    * `Job.cs`: Predstavlja osnovni model poslova (Id, Type, Payload, Priority).
    * `JobType.cs`: Enum koji definiše tipove (`Prime`, `IO`).
    * `JobHandle.cs`: Objekt koji se vraća prilikom slanja posla i pruža `Task` koji će sadržati rezultat.
    * `JobStats.cs`: Model za generisanje i čuvanje izveštaja o performansama sistema.
* **`Core/`**
    * `ProcessingSystem.cs`: Glavni servis za upravljanje redom, radnicima (worker threads) i procesiranje.
    * `Logger.cs`: Sistem za upis događaja i rotaciju XML izveštaja.
* **`Tests/`**
    * `IndustrialProcessing.Tests/`: xUnit projekat sa testovima za proveru asinhronosti, prioriteta, idempotentnosti, timeout-a i izuzetaka.

## ⚙️ Pokretanje

### Sistemski zahtevi
* .NET 6, 7 ili 8 SDK (u zavisnosti od izabrane ciljne platforme)

### Korišćenje (Konzolna aplikacija)
1. Klonirajte repozitorijum.
2. Pobrinite se da se u root direktorijumu nalazi validan `SystemConfig.xml` fajl koji definiše početno stanje.
3. Pokrenite aplikaciju:
```bash
dotnet run --project ProcessingSystem/IndustrialProcessing.csproj
