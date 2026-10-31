# Consuntivo Ore

Piccola applicazione per Windows che ricorda di **consuntivare le ore di lavoro** e prepara un **report settimanale**.

Si avvia insieme a Windows e resta nell'area di notifica (in basso a destra, vicino all'orologio). All'orario scelto mostra una notifica e apre una finestra in cui segnare le ore della giornata.

## Funzionalità

- **Promemoria giornaliero.** Arriva come notifica di Windows all'orario e nei giorni che scegli (predefinito: 17:30, da lunedì a venerdì).
  - Si ripete a intervalli regolari finché non raggiungi le ore previste (predefinito: ogni 30 minuti, fino a 8 h).
  - Se hai già segnato tutte le ore, non ricevi nessun avviso.
  - Nei giorni di ferie o malattia puoi fermarlo con **Non ricordarmelo oggi**, dalla finestra o dal menu dell'icona.
- **Consuntivazione.** Per ogni attività indichi:
  - le ore (es. `1,5`);
  - il cliente, scelto da un elenco che ricorda quelli già usati;
  - una nota su cosa hai fatto.

  Puoi segnare le ore anche per i giorni precedenti e modificare o eliminare le voci già inserite.
- **Report settimanale.** Arriva all'orario impostato (predefinito: venerdì alle 17:45). Se il PC era spento, arriva alla prima accensione della stessa settimana.
  - Mostra le ore giorno per giorno e il totale per cliente, con le percentuali.
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
| Aprire la finestra di consuntivazione | Clic sinistro sull'icona dell'orologio nell'area di notifica |
| Report, impostazioni, uscita | Clic destro sull'icona |
| Salvare un'attività | `Invio`, oppure `Ctrl+Invio` mentre scrivi la nota |
| Modificare una voce | Doppio clic nell'elenco |
| Chiudere la finestra | `Esc` |

Se le notifiche non compaiono, verifica che **Non disturbare** di Windows sia disattivato e prova il pulsante *Invia notifica di prova* nelle impostazioni.

### Dove sono salvati i dati

I dati sono salvati in file JSON in `%APPDATA%\ConsuntivoOre`:

- `entries.json`: le ore registrate;
- `settings.json`: le impostazioni;
- `state.json`: lo stato interno dei promemoria;
- `backup\`: una copia di `entries.json` per ciascuno degli ultimi 7 giorni di utilizzo;
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
UI/                         Finestre di consuntivazione, report e impostazioni
Assets/app.ico              Icona (rigenerabile con tools/MakeIcon.ps1)
installer/                  Script dell'installer Inno Setup
build.ps1                   Compilazione e creazione dell'installer
```

L'avvio automatico usa la chiave di registro `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, valore `ConsuntivoOre`.
