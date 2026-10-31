namespace ConsuntivoOre.Core;

public sealed class TimeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Date { get; set; }
    public decimal Hours { get; set; }
    public string Client { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public sealed class AppSettings
{
    public bool ReminderEnabled { get; set; } = true;
    public TimeOnly ReminderTime { get; set; } = new(17, 30);
    public List<DayOfWeek> ReminderDays { get; set; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
    /// <summary>Ripete il promemoria ogni N minuti finché non si raggiungono le ore previste (0 = mai).</summary>
    public int ReminderRepeatMinutes { get; set; } = 30;
    public decimal DailyTargetHours { get; set; } = 8;

    public bool ReportEnabled { get; set; } = true;
    public DayOfWeek ReportDay { get; set; } = DayOfWeek.Friday;
    public TimeOnly ReportTime { get; set; } = new(17, 45);

    /// <summary>Oltre alla notifica, apre direttamente la finestra di consuntivazione / report.</summary>
    public bool OpenWindowWithNotification { get; set; } = true;

    // Stato interno dello scheduler
    public DateTime? LastReminder { get; set; }
    public DateOnly? LastReportWeek { get; set; }
}
