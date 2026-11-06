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
}

public sealed class TodoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public decimal PlannedHours { get; set; }
    public string Description { get; set; } = "";
    /// <summary>Data entro cui completare il task (facoltativa).</summary>
    public DateOnly? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    /// <summary>Valorizzata quando il task è segnato come completato.</summary>
    public DateTime? CompletedAt { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDone => CompletedAt is not null;
}

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = DataStore.CurrentSchemaVersion;

    public bool ReminderEnabled { get; set; } = true;
    public TimeOnly ReminderTime { get; set; } = new(17, 30);
    public List<DayOfWeek> ReminderDays { get; set; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
    /// <summary>Ripete il promemoria ogni N minuti finché non si raggiungono le ore previste (0 = mai).</summary>
    public int ReminderRepeatMinutes { get; set; } = 30;
    public decimal DailyTargetHours { get; set; } = 8;

    /// <summary>Giorni che contano per le ore settimanali previste nel report.</summary>
    public List<DayOfWeek> WorkDays { get; set; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    public bool ReportEnabled { get; set; } = true;
    public DayOfWeek ReportDay { get; set; } = DayOfWeek.Friday;
    public TimeOnly ReportTime { get; set; } = new(17, 45);

    /// <summary>Oltre alla notifica, apre direttamente la finestra di consuntivazione / report.</summary>
    public bool OpenWindowWithNotification { get; set; } = true;

    /// <summary>Riporta entro limiti validi i valori arrivati da un file modificato a mano.</summary>
    public void Normalize()
    {
        ReminderDays = (ReminderDays ?? []).Distinct().ToList();
        WorkDays = (WorkDays ?? []).Distinct().ToList();
        ReminderRepeatMinutes = Math.Clamp(ReminderRepeatMinutes, 0, 240);
        DailyTargetHours = Math.Clamp(DailyTargetHours, 0, 24);
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
