# Consuntivo Ore

Piccola applicazione per Windows che ricorda di **consuntivare le ore di lavoro** e prepara un **report settimanale**.

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
  - la data entro cui completarlo (facoltativa: le scadenze di oggi o passate sono evidenziate).

  Con **Consuntiva…** registri le ore del task: il giorno proposto è oggi, ma puoi sceglierne un altro. Il nome diventa il cliente e la descrizione la nota. Le ore proposte sono quelle previste ancora da fare (se le hai indicate). Il task consuntivato resta in elenco, in verde, con la spunta e le ore registrate nella colonna **Consuntivato**; puoi consuntivarlo ancora in un altro giorno.

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

Le notifiche restano nel Centro notifiche di Windows e i pulsanti funzionano anche da lì. Se Windows non accetta le notifiche moderne, l'app usa il fumetto dell'area di notifica, che però non ha pulsanti.

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

## Compilare dai sorgenti

Requisiti:

- Windows 10 o 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php), necessario solo per creare l'installer

```powershell
winget install Microsoft.DotNet.SDK.10
winget install JRSoftware.InnoSetup
```

Per avviare l'app in modalità di sviluppo:

```powershell
dotnet run
```

Per creare l'eseguibile e l'installer:

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
Program.cs                  Avvio, gestione dell'istanza singola
TrayApplicationContext.cs   Icona nell'area di notifica, notifiche, controllo degli orari
Core/                       Libreria senza interfaccia: modelli, salvataggio JSON, scheduler, report e CSV
Platform/                   Avvio automatico (registro di Windows)
tests/                      Test di Core
UI/                         Finestra principale (todo e consuntivazione), report e impostazioni
Assets/app.ico              Icona (rigenerabile con tools/MakeIcon.ps1)
installer/                  Script dell'installer Inno Setup
build.ps1                   Compilazione e creazione dell'installer
```

L'avvio automatico usa la chiave di registro `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, valore `ConsuntivoOre`.
