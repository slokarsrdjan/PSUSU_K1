# Industrial Processing System (PSUSU_K1)

Ovaj projekat predstavlja implementaciju **thread-safe** servisa u C#-u pod nazivom `ProcessingSystem`. Sistem simulira asinhronu obradu industrijskih zadataka (Jobs) i zasnovan je na **Producer-Consumer** arhitektonskom šablonu.

Projekat je kreiran kao rešenje zadatka za kolokvijum iz predmeta PSUSU (Projektovanje softvera u sistemima upravljanja) i poštuje stroge zahteve vezane za konkurentno programiranje, objektno-orijentisani dizajn i testiranje.

## Ključne funkcionalnosti

- **Producer-Consumer Arhitektura:** Sistem koristi više niti za istovremeno dodavanje (proizvodnju) i obradu (konzumaciju) zadataka.
- **Thread-Safety:** Siguran rad u višenitnom okruženju obezbeđen je korišćenjem sinhronizacionih mehanizama (poput `lock` i `Monitor` klasa), sprečavajući *race condition*.
- **Idempotentnost:** Implementirana je provera koja osigurava da se isti posao (sa istim ID-jem) nikada ne izvrši više od jednom u sistemu (često podržano upotrebom kolekcija poput `HashSet`).
- **Prioriteti Zadataka:** Poslovi se izvršavaju na osnovu prioriteta, pri čemu manji broj označava veći prioritet.
- **Događaji (Events):** Sistem je event-driven, sa podrškom za pretplaćivanje na događaje putem delegata i lambda izraza (npr. `JobCompleted`, `JobFailed`).
- **Asinhrona Obrada:** Korišćenje `Task` klasa za paralelno izvršavanje specifičnih zadataka (npr. izračunavanje prostih brojeva).
- **Logovanje i Praćenje:** Podrška za čitanje iz XML konfiguracije i generisanje izveštaja (kružni bafer i LINQ upiti).

## Struktura Projekta

- `ProcessingSystem.cs` - Glavna klasa servisa koja upravlja radnim nitima i redom za čekanje.
- `Job.cs` - Model koji predstavlja pojedinačni zadatak (sadrži Guid ID, tip, payload i prioritet).
- `JobHandle.cs` - Pomoćna klasa koja čuva stanje i rezultat obrade (uključujući asinhroni `Task`).
- `JobType.cs` - Enumeracija tipova poslova u sistemu.
- `Logger.cs` / `JobStats.cs` - Klase namenjene za praćenje statistike i beleženje događaja.
- `Program.cs` - Ulazna tačka aplikacije gde se instancira sistem i simulira ubacivanje zadataka.


