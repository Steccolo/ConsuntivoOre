# Consuntivo Ore

Piccola applicazione per Windows (funziona anche su Linux) che ricorda di **consuntivare le ore di lavoro** e prepara un **report settimanale**.

Si avvia insieme a Windows, apre la **Dashboard** e resta nell'area di notifica (in basso a destra, vicino all'orologio). A fine giornata mostra una notifica e apre la scheda in cui segnare le ore.

## Funzionalità

- **Dashboard** (vista iniziale). Due indicatori a semicerchio con le ore di **oggi** e della **settimana** rispetto a quelle previste, e la ripartizione delle ore della settimana **per progetto Jira** o **per issue** (selettore *Progetto / Issue*), con percentuale e ore; le ore non collegate a Jira sono in fondo, alla voce *Senza Jira*.
- **Giornata lavorativa** (impostazioni). Indichi inizio, fine e pausa:
  - le **ore previste** al giorno sono fine − inizio − pausa (es. 9:00–18:00 con 60 minuti di pausa = 8 h);
  - il **promemoria** arriva all'ora di fine giornata;
  - su Tempo la prima voce del giorno parte all'ora di inizio.

  Aggiornando dalla 1.x non cambia nulla: inizio, fine e pausa vengono ricavati dalle impostazioni di prima.
- **Promemoria giornaliero.** Arriva come notifica di Windows a fine giornata, nei giorni che scegli (predefinito: da lunedì a venerdì).
  - Si ripete a intervalli regolari finché non raggiungi le ore previste (predefinito: ogni 30 minuti).
  - Se hai già segnato tutte le ore, non ricevi nessun avviso.
  - Nei giorni di ferie o malattia puoi fermarlo con **Non ricordarmelo oggi**, dalla finestra o dal menu dell'icona.
  - La notifica ha tre pulsanti: **Consuntiva** apre la finestra, **Tra 30 minuti** la rimanda (torna una volta anche se di norma non si ripeterebbe), **Non oggi** la ferma fino a domani.
- **Todo.** Per ogni task indichi:
  - il nome (obbligatorio);
  - una descrizione (obbligatoria), anche **su più righe** con elenchi puntati: in tabella si vede come l'hai scritta;
  - la **priorità** (alta, media, bassa; predefinita media);
  - le ore previste e la data entro cui completarlo (facoltative);
  - la **issue Jira** su cui finiranno le ore (facoltativa, con *Scegli issue…*).

  La tabella è divisa per priorità (*Priorità alta*, *media*, *bassa*): un gruppo compare solo se contiene almeno un todo. Dentro il gruppo, prima le scadenze più vicine. I task scaduti sono in rosso con l'etichetta *Scaduto*, quelli in scadenza oggi hanno l'etichetta *Oggi*; i completati sono in fondo, in verde.

  Con **Consuntiva…** registri le ore del task: il giorno proposto è oggi, ma puoi sceglierne un altro. Il nome diventa il cliente e la descrizione la nota. Se il todo ha una issue Jira, le ore vengono **inviate subito a Tempo** e la voce compare tra i consuntivi come *Inviato*. Il task consuntivato diventa completato; puoi consuntivarlo ancora in un altro giorno.

  **Completato** segna come fatto un task senza consuntivarlo, **Riapri** lo riporta tra quelli da fare ed **Elimina** lo toglie del tutto.
- **Consuntivazione.** Per ogni attività indichi:
  - le ore (es. `1,5`);
  - il cliente, scelto da un elenco che ricorda quelli già usati;
  - una nota su cosa hai fatto.

  Puoi segnare le ore anche per i giorni precedenti e modificare o eliminare le voci già inserite.
- **Report settimanale.** Arriva all'orario impostato (predefinito: venerdì alle 17:45). Se il PC era spento, arriva alla prima accensione della stessa settimana.
  - Mostra le ore giorno per giorno e il totale per cliente, con le percentuali.
  - Ogni campo del dettaglio (cliente, ore, issue, nota) si seleziona da solo: `Ctrl+C`, doppio clic o clic destro → *Copia* lo copiano negli appunti. Le ore si copiano nel formato di Jira (`1,5` → `1h 30m`).
  - Puoi copiarlo come testo, per esempio per incollarlo in una mail.
  - Puoi esportarlo in CSV, che si apre direttamente in Excel.
- **Jira e Tempo** (Jira Cloud con Tempo Timesheets). Si attiva nelle impostazioni, sezione *Jira e Tempo*:
  - indirizzo di Jira (es. `https://azienda.atlassian.net`) ed email con cui accedi;
  - **token API di Jira**: si crea su [id.atlassian.com → Sicurezza → Token API](https://id.atlassian.com/manage-profile/security/api-tokens) (pulsante *Crea token…*);
  - **token di Tempo**: in Jira apri Tempo → *Impostazioni* → *Integrazione API* → *Nuovo token*;
  - *Prova connessione* verifica entrambi e dice in quanti progetti puoi registrare ore.

  Nella scheda *Consuntiva ore* compare la riga **Jira**: con **Scegli issue…** cerchi la issue per chiave (`ABC-123`), per parole del titolo o per chiave del progetto; senza scrivere nulla vedi le issue assegnate a te o su cui hai lavorato di recente. Sono proposte **solo le issue dei progetti in cui hai il permesso di registrare lavoro**.

  Con **Invia subito a Tempo** attivo, salvando la voce le ore vanno subito su Tempo; altrimenti restano *Da inviare* e le invii dopo con **Invia a Tempo** (sotto l'elenco del giorno) o dal report (**Invia a Tempo (N)** manda tutte quelle della settimana). La colonna *Jira* mostra lo stato: *Inviato*, *Da inviare*, *Da aggiornare* (modificata dopo l'invio: il nuovo invio aggiorna la stessa voce su Tempo). Eliminando una voce già inviata puoi scegliere se toglierla anche da Tempo.

  **Importa da Jira** porta nella tabella le ore del giorno registrate su Tempo, anche quelle inserite dal web; all'avvio l'app lo fa da sola per la giornata di oggi. Le voci già inviate e non modificate si allineano al web (se le hai cambiate o cancellate su Tempo, cambiano o spariscono anche qui); quelle che hai modificato in locale e non ancora inviato restano come sono.

  I token restano solo sul PC, nel file `jira.dat` cifrato per il tuo utente Windows.
- **Impostazioni** dall'icona nell'area di notifica: giornata lavorativa, giorni e ripetizione del promemoria, report settimanale, avvio automatico con Windows, Jira e Tempo.

## Installazione

1. Scarica `ConsuntivoOre-Setup-x.y.z.exe` dalla pagina [Releases](../../releases).
2. Avvialo. Non servono permessi di amministratore e .NET non deve essere già installato.
3. Durante l'installazione puoi scegliere se avviare l'app all'accensione del PC (opzione attiva di default) e se creare un'icona sul desktop.

> Il setup non ha una firma digitale, quindi Windows SmartScreen potrebbe mostrare un avviso. Clicca **Ulteriori informazioni → Esegui comunque**.

Per disinstallarla: **Impostazioni di Windows → App → App installate → Consuntivo Ore → Disinstalla**.

## Utilizzo

| Azione | Come |
|---|---|
| Aprire la finestra (todo e consuntivazione) | Clic sinistro sull'icona dell'orologio nell'area di notifica |
| Report, impostazioni, uscita | Clic destro sull'icona |
| Salvare un'attività | `Invio`, oppure `Ctrl+Invio` mentre scrivi la nota |
| Modificare una voce o un todo | Doppio clic nell'elenco |
| Copiare un campo del report | Clic sul campo e `Ctrl+C`, oppure doppio clic |
| Allargare una colonna | Trascina il bordo della sua intestazione: la larghezza viene ricordata |
| Chiudere la finestra | `Esc` |

Le notifiche restano nel Centro notifiche di Windows e i pulsanti funzionano anche da lì. Se Windows non accetta le notifiche, l'app apre direttamente la finestra.

Nel report, la cella selezionata è evidenziata in azzurro: è quella che viene copiata.

Se le notifiche non compaiono, verifica che **Non disturbare** di Windows sia disattivato e prova il pulsante *Invia notifica di prova* nelle impostazioni.

### Dove sono salvati i dati

I dati sono salvati in file JSON in `%APPDATA%\ConsuntivoOre`:

- `entries.json`: le ore registrate;
- `todos.json`: i todo, compresi quelli completati;
- `settings.json`: le impostazioni;
- `state.json`: lo stato interno dei promemoria;
- `jira.dat`: i token di Jira e Tempo, cifrati (leggibili solo dal tuo utente su questo PC);
- `backup\`: una copia di `entries.json` e di `todos.json` per ciascuno degli ultimi 7 giorni di utilizzo;
- `errori.log`: gli eventuali errori.

Se un file risulta illeggibile, all'avvio l'app lo segnala e ne conserva una copia con suffisso `.corrotto-<data>`.

Disinstallando l'app i dati **non** vengono cancellati. Per fare un backup basta copiare la cartella.

Su Linux la cartella è `~/.config/ConsuntivoOre`.

## Compilare dai sorgenti

L'interfaccia è scritta con [Avalonia](https://avaloniaui.net/), quindi si sviluppa e si prova anche su Linux. Il progetto produce due versioni:

- `net10.0-windows10.0.17763.0`: Windows 10 (1809) o 11, con notifiche toast e avvio automatico dal registro;
- `net10.0`: Linux, con notifiche tramite `notify-send` e avvio automatico da `~/.config/autostart`.

Requisiti:

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php), solo su Windows e solo per creare l'installer

```powershell
winget install Microsoft.DotNet.SDK.10
winget install JRSoftware.InnoSetup
```

Per avviare l'app in modalità di sviluppo:

```powershell
dotnet run -f net10.0-windows10.0.17763.0   # Windows
dotnet run -f net10.0                       # Linux
```

Per controllare la grafica senza aprire finestre, `--screenshot <cartella>` salva un'immagine PNG di ogni finestra, con dati di esempio (mai quelli reali):

```bash
dotnet run -f net10.0 -- --screenshot /tmp/schermate
```

Per un eseguibile Linux in un unico file:

```bash
dotnet publish -c Release -f net10.0 -r linux-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish-linux
```

Per creare l'eseguibile e l'installer per Windows:

```powershell
.\build.ps1
```

Lo script produce:

- `publish\ConsuntivoOre.exe`: un unico eseguibile con .NET già incluso;
- `dist\ConsuntivoOre-Setup-<versione>.exe`: l'installer.

Per eseguire i test (funzionano anche fuori da Windows):

```powershell
dotnet test tests/ConsuntivoOre.Tests
```

La versione si imposta in `Directory.Build.props`. Pubblicando un tag `vX.Y.Z`, GitHub Actions esegue i test, crea l'installer e lo allega a una nuova release.

> Se `dotnet publish` segnala l'errore `NU1100`, manca la sorgente NuGet: aggiungila con
> `dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org`

Per chi riprende lo sviluppo: `CLAUDE.md` raccoglie comandi, convenzioni e insidie note; `docs/STORIA.md` racconta richieste, decisioni e problemi affrontati versione per versione.

### Struttura del progetto

```
Program.cs          Avvio, istanza singola, opzioni da riga di comando
App.axaml           Tema e stili comuni
AppController.cs    Icona nell'area di notifica, controllo degli orari, notifiche, apertura delle finestre
Screenshots.cs      Opzione --screenshot (immagini delle finestre con dati di esempio)
Core/               Libreria senza interfaccia: modelli, salvataggio JSON, scheduler, report e CSV, dashboard
Core/Jira/          Client di Jira Cloud e Tempo, invio delle voci, token cifrati
Platform/           Notifiche, avvio automatico, istanza singola, cifratura dei token (Windows e Linux)
ViewModels/         Logica delle finestre: dashboard, todo, consuntivazione, report
Views/              Finestre in XAML: principale (dashboard, todo, consuntivazione), report, impostazioni, dialoghi
tests/              Test di Core
Assets/app.ico      Icona (rigenerabile con tools/MakeIcon.ps1)
installer/          Script dell'installer Inno Setup
build.ps1           Compilazione e creazione dell'installer
```

L'avvio automatico usa, su Windows, la chiave di registro `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, valore `ConsuntivoOre`; su Linux il file `~/.config/autostart/consuntivo-ore.desktop`.
