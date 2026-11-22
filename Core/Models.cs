namespace ConsuntivoOre.Core;

public sealed class TimeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Date { get; set; }
    public decimal Hours { get; set; }
    public string Client { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    /// <summary>Todo da cui è stata consuntivata la voce, se c'è.</summary>
    public Guid? TodoId { get; set; }
    /// <summary>Issue di Jira su cui registrare le ore (facoltativa).</summary>
    public JiraIssueRef? Jira { get; set; }
    /// <summary>Ultimo invio riuscito a Tempo, se c'è.</summary>
    public TempoSync? Tempo { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public JiraStatus JiraStatus => (Jira, Tempo) switch
    {
        (null, null) => JiraStatus.None,
        (not null, null) => JiraStatus.ToSend,
        (null, not null) => JiraStatus.Changed,
        ({ } j, { } t) => t.IssueId == j.Id && t.Date == Date && t.Hours == Hours && t.Note == Note
            ? JiraStatus.Sent
            : JiraStatus.Changed,
    };

    /// <summary>C'è qualcosa da inviare (o togliere) su Tempo.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool NeedsTempoSync => JiraStatus is JiraStatus.ToSend or JiraStatus.Changed;
}

public enum TodoPriority
{
    Low,
    Medium,
    High,
}

public sealed class TodoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public decimal PlannedHours { get; set; }
    /// <summary>Anche su più righe (elenchi puntati compresi): si mostra come è stata scritta.</summary>
    public string Description { get; set; } = "";
    /// <summary>Data entro cui completare il task (facoltativa).</summary>
    public DateOnly? DueDate { get; set; }
    /// <summary>I todo delle versioni precedenti non l'hanno: valgono "media".</summary>
    public TodoPriority Priority { get; set; } = TodoPriority.Medium;
    /// <summary>Issue di Jira del task: consuntivando dal todo le ore vanno lì (facoltativa).</summary>
    public JiraIssueRef? Jira { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    /// <summary>Valorizzata quando il task è segnato come completato.</summary>
    public DateTime? CompletedAt { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDone => CompletedAt is not null;
}

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = DataStore.CurrentSchemaVersion;

    /// <summary>Inizio della giornata lavorativa: su Tempo le voci del giorno partono da qui.</summary>
    public TimeOnly? WorkStart { get; set; }
    /// <summary>Fine della giornata lavorativa: è anche l'orario del promemoria.</summary>
    public TimeOnly? WorkEnd { get; set; }
    /// <summary>Pausa (minuti) tolta dalla giornata per calcolare le ore previste.</summary>
    public int? BreakMinutes { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public TimeOnly DayStart => WorkStart ?? new TimeOnly(9, 0);
    [System.Text.Json.Serialization.JsonIgnore]
    public TimeOnly DayEnd => WorkEnd ?? ReminderTime;

    public bool ReminderEnabled { get; set; } = true;
    /// <summary>Uguale alla fine della giornata (ricalcolato da <see cref="Normalize"/>); resta per i dati vecchi.</summary>
    public TimeOnly ReminderTime { get; set; } = new(17, 30);
    public List<DayOfWeek> ReminderDays { get; set; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
    /// <summary>Ripete il promemoria ogni N minuti finché non si raggiungono le ore previste (0 = mai).</summary>
    public int ReminderRepeatMinutes { get; set; } = 30;
    /// <summary>Ore previste al giorno = fine − inizio − pausa (ricalcolate da <see cref="Normalize"/>).</summary>
    public decimal DailyTargetHours { get; set; } = 8;

    /// <summary>Giorni che contano per le ore settimanali previste nel report.</summary>
    public List<DayOfWeek> WorkDays { get; set; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    public bool ReportEnabled { get; set; } = true;
    public DayOfWeek ReportDay { get; set; } = DayOfWeek.Friday;
    public TimeOnly ReportTime { get; set; } = new(17, 45);

    /// <summary>Oltre alla notifica, apre direttamente la finestra di consuntivazione / report.</summary>
    public bool OpenWindowWithNotification { get; set; } = true;

    /// <summary>Larghezze delle colonne scelte dall'utente, per tabella (l'ultima colonna riempie lo spazio e non c'è).</summary>
    public Dictionary<string, List<double>> ColumnWidths { get; set; } = [];

    public JiraSettings Jira { get; set; } = new();

    /// <summary>Riporta entro limiti validi i valori arrivati da un file modificato a mano.</summary>
    public void Normalize()
    {
        Jira ??= new JiraSettings();
        Jira.Normalize();
        ColumnWidths = (ColumnWidths ?? [])
            .Where(kv => kv.Value is not null)
            .ToDictionary(kv => kv.Key, kv => kv.Value.Select(w => Math.Clamp(double.IsFinite(w) ? w : 0, 30, 2000)).ToList());
        ReminderDays = (ReminderDays ?? []).Distinct().ToList();
        WorkDays = (WorkDays ?? []).Distinct().ToList();
        ReminderRepeatMinutes = Math.Clamp(ReminderRepeatMinutes, 0, 240);
        DailyTargetHours = Math.Clamp(DailyTargetHours, 0, 24);
        NormalizeWorkDay();
    }

    /// <summary>
    /// Inizio, fine e pausa decidono orario del promemoria e ore previste. Con le impostazioni delle versioni
    /// precedenti (senza questi campi) si ricavano da quelle vecchie, in modo che nulla cambi:
    /// inizio = ora della prima voce su Tempo, fine = orario del promemoria, pausa = quanto serve per le ore previste.
    /// </summary>
    private void NormalizeWorkDay()
    {
        var start = WorkStart ?? Jira.DayStart;
        var end = WorkEnd ?? ReminderTime;
        if (end < start) end = start;
        int span = (int)(end - start).TotalMinutes;
        int pause = BreakMinutes ?? span - (int)Math.Round(DailyTargetHours * 60, MidpointRounding.AwayFromZero);

        WorkStart = start;
        WorkEnd = end;
        BreakMinutes = Math.Clamp(pause, 0, span);
        ReminderTime = end;
        DailyTargetHours = Math.Round((span - BreakMinutes.Value) / 60m, 2);
        // Così una versione precedente, riaperta su questi dati, usa lo stesso inizio.
        Jira.DayStart = start;
    }

    /// <summary>Ore previste per inizio, fine e pausa indicati (es. nel form delle impostazioni).</summary>
    public static decimal TargetHours(TimeOnly start, TimeOnly end, int breakMinutes)
    {
        int span = end > start ? (int)(end - start).TotalMinutes : 0;
        return Math.Round(Math.Max(0, span - Math.Max(0, breakMinutes)) / 60m, 2);
    }
}

/// <summary>Stato interno dello scheduler, salvato separatamente dalle impostazioni.</summary>
public sealed class SchedulerState
{
    public DateTime? LastReminder { get; set; }
    public DateOnly? LastReportWeek { get; set; }
    public DateOnly? ReminderSkippedOn { get; set; }
    /// <summary>"Ricordamelo più tardi": il promemoria tace fino a quest'ora, poi torna una volta.</summary>
    public DateTime? ReminderSnoozedUntil { get; set; }
}
