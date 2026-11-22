# Modifiche per miglioramento applicazione ConsuntivoOre
## Di seguito le richieste di improvement diviso per funzionalità
### TODO
- Quando si inserisce un nuovo todo, se si mette il testo della descrizione su più righe si vede tutto su una riga sola. modificare la visualizzazione e permettere di vedere il testo su più righe (come è stato scritto) anche con elenchi puntati
- Aggiungere la possibilità di decidere la priorità del task, oltre alla scadenza.
    La priorità può essere:
        - bassa
        - media
        - alta
    Aggiungere un todo in una classe di priorità che non c'è la fa apparire.

### Consungivazione oraria
- All'apertura dell'applicazione se l'app è connessa a jira si sincronizza e le ore consuntivate della giornata sul web vengono scritte dentro la tabella di consnutivazione oraria

### Nuova vista di default quando si apre il programma (dashboard)
- Aggiungere una dashboard dove è possbile vedere:
 - le ore consuntivate nel giorno grafico tipo gauge
 - Le ore consuntivate nella settimana sempre di tipo gauge
 - percentuale di ore consuntivate divise per task di jira
 - tutte le informazioni sono reperibili agli screen presi da penpot dentro il progetto nella cartella docs/gui-screenshot.
 - Per rifare la grafica qualora non bastassero le librerie utilizzate, basarsi sulla grafica degli screen shot s scrivere la grafica da zero



### impostazioni generali
- agginugere nelle impostazioni la possibilità di selezionare inizio e fine della giornata lavorativa.

### Altro
- Andando avanti nel tempo la registrazione via json di informazioni ed altro potrebbe diventare pesante quanto sarebbe problematica andare a modificare il codice per scrivere i dati dentro un db sqlite indicizzato e compresso?
- uno script che converte l'attuale modalità di salavataggio di conservazione dei dati nel nuovo con sqlite
- per la sicurazze valutare se essite un modo per criptare i dati di sqlite

# IMPORTANTE
data la quantità di mofidiche aprire un nuovo branch e cambiare la versione a 2.0.0
