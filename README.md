# Gamify

Kuriama informacinė sistema „Gamify“ skirta automatiškai analizuoti laisva forma pateiktus naudotojo norus (nuotaiką, žaidėjų skaičių, patirties lygį, biudžetą) bei sudaryti personalizuotus žaidimų rinkinius (grojaraščius / playlists).

„Gamify“ naudoja ASP.NET Core Web API, EF Core su SQLite ir React su Vite.

## 1 sprintas – atlikta

- **GAM-US01** Registracija, prisijungimas ir atsijungimas. Slaptažodžiai saugomi kaip bcrypt maišos, o atsijungus JWT sesija panaikinama.
- **GAM-US02** Savo profilio peržiūra ir redagavimas su laukų validacija.
- **GAM-US03** Platformų ir PC specifikacijų lygio pasirinkimas bei išsaugojimas profilyje. Rekomendacijų filtravimas numatytas US-05.

## Reikalavimai

- .NET 8 SDK. Galima patikrinti su `dotnet --list-sdks`.
- Node.js 24 su npm. Šiame projekte naudojami įrankiai taip pat palaiko Node.js 22 nuo 22.13 ir Node.js 20 nuo 20.19 versijos.

## Vietinis paleidimas

Iš projekto šaknies viename „PowerShell“ terminale paleiskite backend.

```powershell
dotnet tool restore
dotnet ef database update --project backend/Gamify.Api --startup-project backend/Gamify.Api
dotnet run --project backend/Gamify.Api/Gamify.Api.csproj --launch-profile http
```

Kitame terminale iš projekto šaknies paleiskite frontend.

```powershell
Set-Location frontend
Copy-Item .env.example .env
npm.cmd ci
npm.cmd run dev
```

`Copy-Item` vykdykite pirmąkart, kai dar nėra `frontend/.env`. Jame esantis `VITE_API_BASE_URL` pagal numatytąją konfigūraciją yra `http://localhost:5218`. Pakeitę reikšmę perkraukite Vite. `npm.cmd` skirtas Windows terminalui. Kituose terminaluose naudokite `npm`.

- Frontend veikia adresu [http://localhost:5173](http://localhost:5173). Sukūrę paskyrą prisijunkite, o skiltyje „Profilis“ pasirinkite turimas platformas ir, jei naudojate PC, jo specifikacijų lygį. Puslapio apačioje rodoma API būsena.
- Backend būsena pasiekiama per [GET /health](http://localhost:5218/health). Paleistas API grąžina HTTP 200 ir `{"status":"ok"}` nepriklausomai nuo OpenAI nustatymų.
- [Swagger UI](http://localhost:5218/swagger) pasiekiama kūrimo aplinkoje.

„Rider“ atidarykite `Gamify.sln` ir pasirinkite `Gamify.Api` paleidimo profilį `http`. Jei kuriate konfigūraciją rankiniu būdu, darbo katalogu pasirinkite `backend/Gamify.Api`.

Frontend patikros vykdomos iš `frontend` katalogo.

```powershell
npm.cmd run lint
npm.cmd run build
```

## Paskyros ir sesijos

Slaptažodžiai saugomi kaip bcrypt maišos. Prisijungus išduodamas 8 valandų JWT. Atsijungus sesija ištrinama iš duomenų bazės, todėl buvęs JWT nebegalioja.

Pagal numatytąją konfigūraciją serveris kiekvieną kartą paleidžiamas su nauju atsitiktiniu JWT pasirašymo raktu. Jei sesijos turi išlikti perkrovus serverį, backend proceso aplinkoje nustatykite `Jwt__SigningKey` į atsitiktinį bent 32 baitų raktą.

## OpenAI nustatymai

Norėdami įjungti serverio OpenAI klientą, pirmąkart iš projekto šaknies nukopijuokite pavyzdį.

```powershell
Copy-Item backend/Gamify.Api/.env.example backend/Gamify.Api/.env
```

Į `backend/Gamify.Api/.env` įrašykite `OPENAI_API_KEY` ir jūsų OpenAI projekte pasiekiamo modelio ID į `OPENAI_MODEL`. `OPENAI_TIMEOUT_SECONDS` numatytoji reikšmė yra 30. Proceso aplinkos kintamieji turi pirmenybę prieš `.env` reikšmes. Pakeitę nustatymus perkraukite backend.

`OpenAiResponsesClient.GetTextAsync` priima tekstą ir grąžina atsakymo tekstą. Klientą galima gauti per backend priklausomybių injekciją. Raktą laikykite tik backend `.env` faile arba backend proceso aplinkoje. [OpenAI Responses API dokumentacija](https://developers.openai.com/api/reference/resources/responses/methods/create).

## SQLite ir migracijos

Numatytoji prisijungimo eilutė yra `backend/Gamify.Api/appsettings.json` faile, po `ConnectionStrings:Gamify`. Santykinis SQLite failo kelias skaičiuojamas nuo backend turinio katalogo. Naudojant nurodytą CLI komandą arba „Rider“ profilį, failo kelias yra `backend/Gamify.Api/gamify.db`. Failas sukuriamas pirmąkart atidarius ryšį, jei jo dar nėra.

Pradinė migracija `InitialCreate` sukuria paskyrų, sesijų ir platformų lenteles bei įrašo keturias platformas. Atnaujinę duomenų modelį, iš projekto šaknies sukurkite ir pritaikykite naują migraciją.

```powershell
dotnet tool restore
dotnet ef migrations add NaujosMigracijosPavadinimas --project backend/Gamify.Api --startup-project backend/Gamify.Api
dotnet ef database update --project backend/Gamify.Api --startup-project backend/Gamify.Api
```

[EF Core CLI dokumentacija](https://learn.microsoft.com/ef/core/cli/dotnet).
