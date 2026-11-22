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

## 1.5.0 — Jira e Tempo (07/11/2026)

Prima sessione sviluppata su **Windows**.

**Richiesta**: collegarsi a Jira con il proprio utente e consuntivare le ore direttamente nei progetti in cui
si ha il permesso di farlo.

**Scelte dell'utente** (domande fatte prima di scrivere codice): Jira **Cloud**; le ore vanno su
**Tempo Timesheets** (non il log work nativo); collegamento alla issue nel form con invio **subito o dopo dal
report**; issue scelta con una **ricerca diretta** (non progetto → issue, non issue fissa per cliente).

**Decisioni**

- Due credenziali: email + token API di Jira (utente, progetti consentiti, ricerca) e token di Tempo (worklog).
  Token in `jira.dat` cifrato con DPAPI, non in `settings.json`.
- Progetti consentiti: permesso `WORK_ON_ISSUES`. Ricerca: chiave `ABC-123` (letta direttamente e scartata se
  in un progetto non consentito), chiave di progetto, parole del titolo con `*`; vuota = issue recenti mie.
- La voce ricorda la issue (`Jira`: id, chiave, titolo, progetto) e l'ultimo invio (`Tempo`: id del worklog e
  dati inviati). Lo stato (da inviare / inviata / da aggiornare) si ricava confrontandoli. Campi solo aggiuntivi:
  `SchemaVersion` invariata.
- Modifica dopo l'invio → aggiornamento dello stesso worklog (se su Tempo non c'è più se ne crea uno nuovo);
  issue tolta → worklog cancellato; eliminazione → si chiede se togliere anche da Tempo.
- Su Tempo le voci del giorno vanno in fila dalle 9:00 (configurabile) nell'ordine di inserimento.
- Descrizione vuota non accettata da Tempo → si usa il titolo della issue.
- Il cliente resta obbligatorio; se vuoto, scegliendo la issue si propone il nome del progetto.
- La issue resta nel form per la voce successiva, come il cliente.

**Non verificato** (nessun Jira/Tempo reale disponibile durante lo sviluppo): tutte le chiamate di rete sono
provate solo con un server finto nei test. In particolare: attributi di lavoro obbligatori in Tempo (es.
"Account") — se configurati l'invio fallisce con il messaggio di Tempo; istanze Tempo con dati in UE (indirizzo
API modificabile nelle impostazioni); limite di 30 risultati nella ricerca.

**Rilascio** (07/11/2026): commit `110eef6`, tag `v1.5.0`; CI riuscita su tag e `main`, release
https://github.com/Steccolo/ConsuntivoOre/releases/tag/v1.5.0 con `ConsuntivoOre-Setup-1.5.0.exe` (~51 MB).

**Ambiente Windows dell'utente** (emerso in questa sessione)

- **Inno Setup non installato, per scelta**: l'utente non vuole "sporcare" la macchina; lo installerà se servirà.
  `build.ps1` quindi si ferma: in locale si genera solo l'exe con il `dotnet publish` manuale (vedi `CLAUDE.md`)
  e l'installer lo crea la CI al tag. Inno Setup non si installa da Visual Studio (è un programma a parte:
  `winget install JRSoftware.InnoSetup`).
- **`git` non è nel PATH**: si usa quello incluso in Visual Studio Community
  (`C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe`);
  c'è anche quello di GitHub Desktop.
- **`gh` non è installato**: la CI e la release si controllano con l'API pubblica di GitHub
  (`Invoke-RestMethod https://api.github.com/repos/Steccolo/ConsuntivoOre/actions/runs` e `.../releases/tags/<tag>`).

---

## 2.0.0 — Dashboard, priorità, importazione da Jira, nuova grafica (22/11/2026, branch `versione-2.0`)

**Richieste** (in `docs/improvement.md`, mockup Penpot in `docs/gui-screenshot/`)

1. Todo: descrizione su più righe visibile com'è scritta (anche elenchi puntati); priorità bassa/media/alta,
   con il gruppo di una priorità che compare solo quando c'è almeno un todo.
2. All'avvio, se collegata a Jira, l'app importa le ore della giornata registrate sul web.
3. Dashboard come vista iniziale: indicatori a semicerchio per oggi e per la settimana, percentuali per Jira.
4. Impostazioni: inizio e fine della giornata lavorativa.
5. Valutare SQLite (indicizzato, compresso, cifrato) al posto dei JSON.
6. Aggiunta durante la discussione: nel todo si può indicare la issue Jira; consuntivando dal todo la voce va
   subito su Tempo e compare tra i consuntivi come "Inviato".
7. Font: 700 titolo di pagina (32 px) e totale (16 px); 600 titoli delle card, etichette, bottoni, scheda attiva,
   intestazioni delle tabelle; 400 il resto.

**Decisioni prese con l'utente**

- Importazione: **il web vince** sulle voci già inviate e non modificate (si aggiornano o, se cancellate su Tempo,
  si tolgono); le modifiche locali non ancora inviate restano. Le voci mancanti si creano già "Inviato".
- Fine giornata: serve **sia** come orario del promemoria **sia** per le ore previste (fine − inizio − pausa).
  Le impostazioni vecchie si convertono senza cambiare nulla: inizio = ora della prima voce su Tempo,
  fine = orario del promemoria, pausa = quanto serve per riavere le stesse ore previste.
- Dashboard: percentuali **per progetto e per issue**, con un selettore; le ore senza Jira in fondo ("Senza Jira").
- Grafica: si resta su Avalonia (togliere tutto il tema avrebbe voluto dire riscrivere anche calendario, menu a
  tendina, campo numerico: 2–3 volte il lavoro e codice non collaudato). **Via di mezzo** scelta dall'utente:
  da zero bottoni, schede, card, etichette, testi, indicatori (`Views/Meters.cs`), intestazioni e celle delle
  tabelle; Fluent ricolorato per campi, date, orari, menu, caselle e righe/scorrimento del DataGrid.
  Il DataGrid resta perché dà gratis colonne ridimensionabili e copia della cella (l'utente aveva chiesto perché
  "si dovesse perdere il trascinamento": non si perde).
- Gruppi di priorità come righe d'intestazione nella tabella (come i giorni nel report), non con il raggruppamento
  interno del DataGrid, che non si può controllare (il suo tema è compilato nella libreria).
- **SQLite abbandonato** (compressione e cifratura comprese): con ~2.500 voci l'anno il JSON resta sotto 1 MB/anno;
  SQLite gratuito non comprime; la cifratura avrebbe legato i dati all'utente Windows (reinstallando si perdono
  anche i backup). Per proteggere i file: BitLocker.
- Carattere: Inter (già incluso) con i pesi richiesti; il mockup usa Source Sans, si può cambiare in un punto solo.
- Giorno della scheda Consuntiva spostato a destra del titolo (nel mockup è sotto) per lasciare più righe alla tabella.

**Problemi**

- `Smart App Control` (attivo sul PC dell'utente) ha bloccato a metà lavoro la DLL di sviluppo appena compilata
  (evento CodeIntegrity 3077): le ultime correzioni grafiche non sono state viste negli screenshot. Le prime
  esecuzioni della stessa sessione erano passate: Windows decide file per file.

**Non verificato**: importazione con un Tempo reale (provata solo con il server finto dei test); aspetto finale dopo
gli ultimi ritocchi (riga Jira della scheda Consuntiva, nome del todo con l'etichetta accanto, altezza finestra);
calendario e menu a tendina aperti (popup non catturati negli screenshot).

---

## Da verificare / idee aperte

- **Jira/Tempo con un account reale** (vedi 1.5.0). Idee: issue predefinita per todo, attributi di lavoro Tempo.

- **CI con release già esistente**: correzione mai messa alla prova (vedi sopra).
- **Linux**: notifiche `notify-send` con azioni e icona nell'area di notifica non provate su un desktop reale
  (su GNOME serve l'estensione AppIndicator). Su Linux si è verificato solo l'avvio e l'istanza unica.
- **Tema scuro**: l'app forza il tema chiaro; i colori delle righe andrebbero adattati.
- "Tra 30 minuti" ha una durata fissa (`Scheduler.SnoozeMinutes`); si potrebbe renderla configurabile.
- La CI produce solo l'installer Windows; un eseguibile Linux in release non è stato richiesto.
