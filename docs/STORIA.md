# Storia dello sviluppo

Diario delle richieste dell'utente, delle decisioni prese e dei problemi affrontati, versione per versione.
Serve a chi riprende il lavoro (anche un assistente AI su un altro PC) per capire **perché** il codice è così.
Le regole pratiche per lavorare sul progetto sono in `CLAUDE.md`.

Sviluppo fatto su **Linux** (senza .NET di sistema), app usata e provata dall'utente su **Windows**.

---

## Fino alla 1.1.0 (31/10/2026)

Prima sessione: messa in sicurezza dell'app WinForms originale.

- **Dati**: niente avvio silenzioso con file illeggibili (messi da parte come `.corrotto-<data>`), annullamento
  delle modifiche in memoria se il salvataggio fallisce, scrittura forzata su disco, backup giornalieri (7),
  numero di versione del formato, stato dello scheduler separato in `state.json`.
- **Promemoria**: non azzera il form mentre si scrive, "Non ricordarmelo oggi", nessuna ripetizione senza
  obiettivo di ore, notifiche in coda.
- **Report**: anno ISO nel nome del CSV, errori di esportazione gestiti, giorni lavorativi separati dai giorni
  del promemoria.
- **Struttura**: `Core` in una libreria senza interfaccia, `Scheduler` puro, test xUnit, CI GitHub Actions.

Checklist di prova manuale consegnata all'utente (tutto confermato funzionante il 06/11): file corrotto,
salvataggio fallito, backup, compatibilità dati vecchi, form non azzerato dal promemoria, "Non ricordarmelo oggi",
niente ripetizioni senza obiettivo, notifiche in coda, CSV a cavallo d'anno, CSV aperto in Excel,
giorni lavorativi, avvio automatico e installer.

---

## 1.2.0 — Todo e copia dei campi del report (06/11/2026)

**Richieste**

1. Nel report settimanale cliccando una riga si selezionava tutta: l'utente vuole selezionare **il singolo campo**
   (ore, cliente, nota) per copiarlo in **Jira** senza riscriverlo.
2. Nuova parte **Todo**: all'accensione del PC il programma si apre sulla scheda Todo. Ogni todo ha nome,
   ore previste, descrizione, data di scadenza facoltativa. Un pulsante consuntiva direttamente dal todo,
   **nel giorno corrente** (non nella data di scadenza): nome → cliente, descrizione → nota.

**Decisioni**

- Report: `ListView` sostituito da `DataGridView` con selezione per cella; Ctrl+C, doppio clic e menu Copia.
  La nota copiata è quella completa su più righe.
- Todo: finestra principale con due schede (Todo, Consuntiva ore). All'avvio (anche con `--startup`) si apre
  su Todo; il promemoria apre Consuntiva ma non toglie l'utente dalla scheda Todo se sta scrivendo un todo.
- `TimeEntry.TodoId` collega le ore al todo (colonna con le ore già consuntivate). `todos.json` con backup.

**Richieste successive nella stessa versione**

- Obbligatori solo **nome e descrizione**; ore previste facoltative ("non sempre so quanto impiego").
- Ore copiate nel formato di Jira: `1,5` → `1h 30m` (`Fmt.Duration`, testato).

**Problemi**

- Sul PC di sviluppo non c'era .NET: installato in una cartella temporanea con `dotnet-install.sh`.
  Il target Windows si compila da Linux grazie a `EnableWindowsTargeting`.
- Inno Setup non esiste su Linux: l'installer lo crea solo la CI (o `build.ps1` su Windows).
- `git status` mostrava 17 file modificati: erano solo permessi (644 → 755). Risolto con `core.fileMode=false`;
  nei commit si escludono i cambi di permessi.

---

## CI: release già esistente (06/11/2026)

La CI della v1.1.0 risultava fallita. Causa: il tag era stato pubblicato, poi la storia riscritta e il tag
spostato su un nuovo commit con **lo stesso contenuto** (stesso albero `3c9a379`); il secondo giro falliva su
`gh release create` perché la release esisteva già. Il setup pubblicato era corretto.

Correzione: se la release esiste, `gh release upload --clobber` sostituisce solo il setup.
**Non ancora verificata in un caso reale** (servirebbe spostare un tag). Il workflow eseguito per un tag è quello
del commit taggato, quindi ripubblicare un tag vecchio userebbe il workflow vecchio.

---

## 1.3.0 – 1.3.2 — Notifiche con pulsanti e stato dei todo (06/11/2026)

**Domanda dell'utente**: conviene passare a un linguaggio più leggero (Rust, Go, C++) o a Qt?
Risposta data: per un'app ferma nell'area di notifica la velocità non cambia; le notifiche native di Windows
si usano da qualsiasi linguaggio. Consigli: notifiche con pulsanti restando in C#, eventuale nuova grafica con
Avalonia (riusa `Core`, gira anche su Linux). Qt da C# non era maturo (Qt Bridge in Technology Preview).

**1.3.0**

- Notifiche toast di Windows con pulsanti (`Microsoft.Toolkit.Uwp.Notifications`, target
  `net10.0-windows10.0.17763.0`): promemoria con **Consuntiva / Tra 30 minuti / Non oggi**, report con
  **Apri il report**. Funzionano anche dal Centro notifiche e ad app chiusa (Windows la avvia con `-ToastActivated`).
  Il disinstallatore chiama `--uninstall-notifications`.
- "Tra 30 minuti": `SchedulerState.ReminderSnoozedUntil`; il promemoria torna **una volta** anche se di norma
  non si ripeterebbe; ignorato se di un altro giorno; non scavalca l'obiettivo raggiunto (testato).
- Todo: "Completato" non fa più sparire la riga (verde, con **Riapri**); "Consuntiva…" permette di scegliere il
  giorno (di default oggi). Consuntivare un todo lo segna completato; si può consuntivare ancora.

**1.3.1 / 1.3.2 — colori richiesti dall'utente** (il verde chiaro "si vedeva poco")

| Stato | Sfondo | Testo |
|---|---|---|
| Completato o consuntivato | verde chiaro `#DCFCE7` | grassetto verde scuro `#14532D` |
| Scade oggi | giallo `#FEF9C3` | grassetto **nero** (prima arancione, cambiato su richiesta) |
| Scaduto da ≥ 1 giorno | giallo `#FEF9C3` | grassetto rosso `#DC2626` |

Colonna "Consuntivato": solo le ore (`1,5 h`), la spunta ✓ è stata tolta su richiesta.

---

## Scelta della libreria grafica (06/11/2026)

Requisiti delle tabelle emersi: copia della singola cella, colori per riga, intestazioni/gruppi per giorno,
colonne ridimensionabili. Confronto fatto: Avalonia (DataGrid MIT), WPF + WPF-UI (solo Windows), WinUI 3
(nessun DataGrid ufficiale), Qt Bridge per C# (preview), Slint/egui/Fyne (tabelle da costruire a mano).
Attenzione: in Avalonia il **TreeDataGrid** è diventato commerciale dopo la 11.2; il normale **DataGrid** è MIT.

Scelta: **Avalonia**, soprattutto perché lo sviluppo avviene su Linux. Prima un prototipo (schede Todo e
Consuntiva) provato dall'utente, poi il porting completo.

---

## 1.4.0 — Interfaccia in Avalonia (06/11/2026)

Tutta l'app riscritta in Avalonia 12 con MVVM (CommunityToolkit.Mvvm), stesse funzioni della 1.3.2.
`Core`, test, formato dei dati e installer invariati: la 1.4 legge i dati della 1.3 così come sono.

- Due target: Windows (toast, avvio automatico dal registro) e Linux (`notify-send` con azioni,
  `~/.config/autostart/consuntivo-ore.desktop`).
- Istanza unica: stesso mutex (`ConsuntivoOreMutex`, usato dall'installer) + named pipe per riaprire la finestra
  (il named `EventWaitHandle` di prima non esiste su Linux).
- Se Windows rifiuta il toast, si apre direttamente la finestra (Avalonia non ha il "fumetto" della tray).
- `--screenshot <cartella>`: disegna tutte le finestre con dati di esempio senza mostrarle. È stato lo strumento
  principale per controllare la grafica da Linux e ha fatto trovare diversi errori (sotto).

**Problemi incontrati**

- Testo invisibile nelle righe "normali" dei todo: lo stile assegnava un `Foreground` null. → colori espliciti.
- Intestazioni e date tagliate, riquadri invisibili (le risorse colore di WinUI non esistono in Avalonia),
  tab con carattere enorme → stili espliciti in `App.axaml`.
- Date in formato americano sulla macchina di sviluppo → cultura `it-IT` forzata all'avvio.
- API cambiate in Avalonia 12 (`PlaceholderText`, estensione `SetTextAsync`, `Bitmap.Save`).
- DataGrid senza selezione per cella → `CurrentColumn` + stile `DataGrid.cells`.
- Vulnerabilità NU1904 su `System.Drawing.Common` 4.7.0 (dipendenza del toolkit toast, prima coperta da WinForms)
  → riferimento esplicito alla 10.0.x.
- La pubblicazione per Windows lasciava `libSkiaSharp.pdb` e `libHarfBuzzSharp.pdb` (>100 MB) accanto all'exe →
  rimossi da `build.ps1` ed esclusi nell'installer.
- `dotnet publish` con due target richiede `-f` → aggiornato `build.ps1`.

## 1.4.1 – 1.4.2 — Colonne (06/11/2026)

- 1.4.1: l'utente ha notato che le colonne non si ridimensionavano: nel DataGrid di Avalonia
  `CanUserResizeColumns` è false di default → stile globale.
- 1.4.2: su richiesta, le larghezze vengono **ricordate** (`Views/ColumnLayout.cs`, `AppSettings.ColumnWidths`,
  salvate alla chiusura della finestra; le colonne "*" restano libere; valori riportati tra 30 e 2000, testato).

Installata e confermata funzionante dall'utente.

---

## Da verificare / idee aperte

- **CI con release già esistente**: correzione mai messa alla prova (vedi sopra).
- **Linux**: notifiche `notify-send` con azioni e icona nell'area di notifica non provate su un desktop reale
  (su GNOME serve l'estensione AppIndicator). Su Linux si è verificato solo l'avvio e l'istanza unica.
- **Tema scuro**: l'app forza il tema chiaro; i colori delle righe andrebbero adattati.
- "Tra 30 minuti" ha una durata fissa (`Scheduler.SnoozeMinutes`); si potrebbe renderla configurabile.
- La CI produce solo l'installer Windows; un eseguibile Linux in release non è stato richiesto.
