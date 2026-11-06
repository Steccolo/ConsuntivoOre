# Consuntivo Ore

Piccola applicazione per Windows (funziona anche su Linux) che ricorda di **consuntivare le ore di lavoro** e prepara un **report settimanale**.

Si avvia insieme a Windows, apre la scheda **Todo** e resta nell'area di notifica (in basso a destra, vicino all'orologio). All'orario scelto mostra una notifica e apre la scheda in cui segnare le ore della giornata.

## Funzionalità

- **Promemoria giornaliero.** Arriva come notifica di Windows all'orario e nei giorni che scegli (predefinito: 17:30, da lunedì a venerdì).
  - Si ripete a intervalli regolari finché non raggiungi le ore previste (predefinito: ogni 30 minuti, fino a 8 h).
  - Se hai già segnato tutte le ore, non ricevi nessun avviso.
  - Nei giorni di ferie o malattia puoi fermarlo con **Non ricordarmelo oggi**, dalla finestra o dal menu dell'icona.
  - La notifica ha tre pulsanti: **Consuntiva** apre la finestra, **Tra 30 minuti** la rimanda (torna una volta anche se di norma non si ripeterebbe), **Non oggi** la ferma fino a domani.
- **Todo.** All'avvio la finestra si apre sulla scheda *Todo*. Per ogni task indichi:
  - il nome (obbligatorio);
  - una descrizione (obbligatoria);
  - le ore previste (facoltative);
  - la data entro cui completarlo (facoltativa). I task in scadenza oggi sono evidenziati in giallo con il testo nero, quelli scaduti in giallo con il testo rosso.

  Con **Consuntiva…** registri le ore del task: il giorno proposto è oggi, ma puoi sceglierne un altro. Il nome diventa il cliente e la descrizione la nota. Le ore proposte sono quelle previste ancora da fare (se le hai indicate). Il task consuntivato resta in elenco, evidenziato in verde, con le ore registrate nella colonna **Consuntivato**; puoi consuntivarlo ancora in un altro giorno.

  Con **Completato** segni come fatto un task senza consuntivarlo: anche lui resta in elenco, in verde, in fondo ai task da fare; **Riapri** lo riporta tra quelli da fare ed **Elimina** lo toglie del tutto.
- **Consuntivazione.** Per ogni attività indichi:
  - le ore (es. `1,5`);
  - il cliente, scelto da un elenco che ricorda quelli già usati;
  - una nota su cosa hai fatto.

  Puoi segnare le ore anche per i giorni precedenti e modificare o eliminare le voci già inserite.
- **Report settimanale.** Arriva all'orario impostato (predefinito: venerdì alle 17:45). Se il PC era spento, arriva alla prima accensione della stessa settimana.
  - Mostra le ore giorno per giorno e il totale per cliente, con le percentuali.
  - Ogni campo del dettaglio (cliente, ore, nota) si seleziona da solo: `Ctrl+C`, doppio clic o clic destro → *Copia* lo copiano negli appunti, per esempio per incollarlo in Jira. Le ore si copiano nel formato di Jira (`1,5` → `1h 30m`).
  - Puoi copiarlo come testo, per esempio per incollarlo in una mail.
  - Puoi esportarlo in CSV, che si apre direttamente in Excel.
- **Impostazioni** dall'icona nell'area di notifica: orari, giorni, ore previste, frequenza del promemoria, giorni lavorativi usati per calcolare le ore settimanali previste, avvio automatico con Windows.

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

### Struttura del progetto

```
Program.cs          Avvio, istanza singola, opzioni da riga di comando
App.axaml           Tema e stili comuni
AppController.cs    Icona nell'area di notifica, controllo degli orari, notifiche, apertura delle finestre
Screenshots.cs      Opzione --screenshot (immagini delle finestre con dati di esempio)
Core/               Libreria senza interfaccia: modelli, salvataggio JSON, scheduler, report e CSV
Platform/           Notifiche, avvio automatico e istanza singola (Windows e Linux)
ViewModels/         Logica delle finestre: todo, consuntivazione, report
Views/              Finestre in XAML: principale (todo e consuntivazione), report, impostazioni, dialoghi
tests/              Test di Core
Assets/app.ico      Icona (rigenerabile con tools/MakeIcon.ps1)
installer/          Script dell'installer Inno Setup
build.ps1           Compilazione e creazione dell'installer
```

L'avvio automatico usa, su Windows, la chiave di registro `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, valore `ConsuntivoOre`; su Linux il file `~/.config/autostart/consuntivo-ore.desktop`.
