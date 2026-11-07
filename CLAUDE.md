# Consuntivo Ore — guida per chi sviluppa (persone o assistenti AI)

App da area di notifica per consuntivare le ore di lavoro: todo, promemoria con notifiche, report settimanale.
Interfaccia in **Avalonia 12** (.NET 10), principalmente per **Windows**, funziona anche su Linux.
Per l'uso vedi `README.md`; per la storia di richieste, decisioni e problemi vedi **`docs/STORIA.md`** (leggilo prima di modifiche importanti).

## Lingua e stile

- Interfaccia, messaggi, commenti nel codice, README e messaggi di commit: **in italiano**.
- Commit: titolo breve all'imperativo/presente con la versione tra parentesi quando c'è una release
  (es. `Ricorda le larghezze delle colonne (1.4.2)`), corpo con elenco puntato del perché.
- Formato numeri e date sempre italiano tramite `Fmt` (`Fmt.Hours` → `1,5`, `Fmt.Date` → `06/11/2026`);
  `Program.Main` forza la cultura `it-IT` anche su sistemi in un'altra lingua.

## Come lavora l'utente (importante)

1. Chiede una modifica → si implementa, si compila, si eseguono i test, si genera l'eseguibile da provare.
2. L'utente lo prova su Windows sostituendo l'exe in `%LOCALAPPDATA%\Programs\ConsuntivoOre\`.
3. Solo quando lo chiede esplicitamente: **commit, push e tag `vX.Y.Z`** (la CI crea la release con l'installer).
   Non fare push o tag senza richiesta. Dopo il tag, seguire la CI e comunicare esito e link della release.
- Ogni modifica rilasciata alza la versione in `Directory.Build.props` (unica fonte, usata anche dall'installer).
  Patch per correzioni/ritocchi, minor per funzioni nuove o cambi sostanziali (la 1.4 è il passaggio ad Avalonia).
- Preferisce risposte concrete con elenco di cosa provare; dichiarare sempre cosa NON è stato verificato.

## Comandi

```powershell
# Test di Core (girano ovunque)
dotnet test tests/ConsuntivoOre.Tests

# Avvio in sviluppo: il progetto ha DUE target, -f è obbligatorio
dotnet run -f net10.0-windows10.0.17763.0     # Windows
dotnet run -f net10.0                         # Linux

# Exe + installer per Windows (richiede Inno Setup 6)
.\build.ps1     # → publish\ConsuntivoOre.exe e dist\ConsuntivoOre-Setup-<versione>.exe

# Immagini PNG di tutte le finestre con dati di esempio (cartella temporanea, mai i dati reali)
dotnet run -f net10.0 -- --screenshot <cartella>
```

Pubblicazione manuale dell'exe Windows (identica a `build.ps1`):

```
dotnet publish ConsuntivoOre.csproj -c Release -f net10.0-windows10.0.17763.0 -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
```

Poi cancellare `publish\*.pdb` (simboli di SkiaSharp/HarfBuzz, >100 MB: `build.ps1` lo fa già, l'installer li esclude).

## Struttura

```
Program.cs          Avvio: cultura it-IT, --startup, --uninstall-notifications, --screenshot, istanza unica
App.axaml(.cs)      Tema Fluent (chiaro), stili comuni (card, hint, error, DataGrid), caricamento dati
AppController.cs    Icona nell'area di notifica, timer (20 s), promemoria/report, notifiche, apertura finestre
Screenshots.cs      Modalità --screenshot (Avalonia.Headless)
Core/               Libreria senza UI: modelli, DataStore (JSON), Scheduler, ReportBuilder, Fmt, Log
Core/Jira/          JiraClient (Jira Cloud REST v3 + Tempo API v4), JiraService (invio/sync), CredentialStore
Platform/           Toast (Windows: toolkit UWP; Linux: notify-send), Autostart, SingleInstance, Secrets (DPAPI)
ViewModels/         MVVM con CommunityToolkit.Mvvm: Todo, Entry (consuntiva), Report, Main
Views/              Finestre XAML: MainWindow (schede Todo/Consuntiva), ReportWindow, SettingsWindow, dialoghi
tests/              xUnit su Core
installer/          Inno Setup (AppMutex=ConsuntivoOreMutex, voce Run, --uninstall-notifications)
.github/workflows/  CI su windows-latest: test, build.ps1, release sui tag v*
```

`Core` non dipende dall'interfaccia: logica e salvataggio vanno lì e si testano lì.
Lo `Scheduler` è puro (nessun effetto collaterale): ogni nuova regola dei promemoria va testata in `SchedulerTests`.

## Dati (non romperli)

- Cartella: `%APPDATA%\ConsuntivoOre` (Linux: `~/.config/ConsuntivoOre`).
  File: `entries.json`, `todos.json`, `settings.json`, `state.json`, `backup\`, `errori.log`.
- Gli utenti aggiornano installando sopra: **i dati delle versioni precedenti devono continuare a funzionare**.
  Campi nuovi solo aggiuntivi e con valore predefinito; se il formato cambia davvero, alzare
  `DataStore.CurrentSchemaVersion` e gestire la migrazione (vedi la lettura del vecchio `entries.json` come array).
- Salvataggi atomici (`.tmp` + move) con annullamento in memoria se falliscono; file illeggibili messi da parte
  come `.corrotto-<data>`, mai sovrascritti. Rispettare questo schema per ogni nuovo file.
- Valori letti da file modificabili a mano passano da `AppSettings.Normalize()`.
- `jira.dat`: token di Jira e Tempo cifrati con DPAPI (utente corrente). Se non si decifra (altro PC/utente)
  si considera vuoto: i token si rigenerano, non serve metterlo da parte. Mai token in `settings.json`.

## Jira e Tempo

- Jira **Cloud** (email + token API, Basic) per utente, progetti e ricerca; **Tempo** (token Bearer, `api.tempo.io/4`)
  per i worklog. Tempo vuole l'**id numerico** della issue e l'`accountId` dell'autore, non la chiave.
- "Progetti in cui posso consuntivare" = `POST /rest/api/3/permissions/project` con `WORK_ON_ISSUES`;
  la ricerca usa `POST /rest/api/3/search/jql` (il vecchio `/search` è dismesso).
- Stato della voce calcolato da `TimeEntry.Jira` + `TimeEntry.Tempo` (copia dei dati inviati): niente flag da tenere
  allineati. Ogni invio passa da `JiraService.SyncAsync` (lucchetto contro doppi invii); i test usano un
  `HttpMessageHandler` finto (`JiraTests`), nessuna chiamata reale.

## Insidie note (già incontrate)

- **Avalonia 12 ≠ 11**: `Watermark` è obsoleto → `PlaceholderText`; appunti con `Clipboard.SetTextAsync` è
  un'estensione in `Avalonia.Input.Platform` (serve il `using`); `Bitmap.Save(string)` è obsoleto.
  Se un'API non compila, cercare nel file XML della documentazione del pacchetto in `~/.nuget/packages`.
- **DataGrid**: niente selezione per singola cella. Il report usa `CurrentColumn` + riga selezionata e lo stile
  `DataGrid.cells` (in `App.axaml`) che nasconde la selezione di riga ed evidenzia `DataGridCell:current`.
  `CanUserResizeColumns` è **false di default** (attivato con uno stile globale). Le larghezze sono ricordate da
  `Views/ColumnLayout.cs` in `settings.json` (`ColumnWidths`).
- **Colori delle righe** con binding nello stile `DataGridRow`: un pennello `null` rende il testo **invisibile**;
  usare sempre colori espliciti (tema chiaro forzato in `App.axaml`).
- **Multi-target**: `dotnet publish`/`run` senza `-f` falliscono. Il codice solo Windows sta sotto `#if WINDOWS`
  (definito per il target `-windows`). `EnableWindowsTargeting` permette di compilare il target Windows da Linux.
- **Microsoft.Toolkit.Uwp.Notifications** (toast) dipende da `System.Drawing.Common` 4.7.0, vulnerabile:
  c'è un riferimento esplicito a una versione aggiornata, altrimenti la build fallisce (warning trattati come errori).
- `TreatWarningsAsErrors` è attivo per tutto (`Directory.Build.props`).
- Notifiche: il clic arriva su un thread in background → `Dispatcher.UIThread.Post`. Se l'app era chiusa,
  Windows la avvia con `-ToastActivated` e l'azione arriva dopo `Toast.Initialize`.
- Istanza unica: mutex `ConsuntivoOreMutex` (non rinominarlo: lo usa l'installer) + named pipe per il "mostra finestra".
- CI: il passo di release, se la release del tag esiste già (tag spostato), sostituisce solo il setup.
  Il workflow usato è quello **del commit taggato**.

## Sviluppare su Linux (macchina dell'utente)

- Non c'è un .NET di sistema: installarlo in una cartella temporanea con `dotnet-install.sh --channel 10.0`.
- La grafica si controlla con `--screenshot` e leggendo i PNG.
- Per provare l'avvio reale senza toccare i dati dell'utente: `XDG_CONFIG_HOME=<cartella temporanea> ./ConsuntivoOre`.
- Il repo ha `core.fileMode=false` (i permessi dei file risultavano cambiati senza motivo).

## Sviluppare su Windows

- `dotnet run -f net10.0-windows10.0.17763.0` usa i **dati reali** in `%APPDATA%\ConsuntivoOre`: fare una copia
  della cartella prima di esperimenti rischiosi. In Debug l'avvio automatico non tocca il registro.
- Qui si possono verificare davvero notifiche toast, icona nell'area di notifica e clic: sono le parti che da Linux
  sono state solo compilate (vedi "Da verificare" in `docs/STORIA.md`).
