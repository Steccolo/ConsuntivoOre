namespace ConsuntivoOre.Core;

/// <summary>Decide quando inviare promemoria e report. Non ha effetti collaterali.</summary>
public static class Scheduler
{
    public static bool IsReminderDue(AppSettings s, SchedulerState state, DateTime now, decimal hoursToday)
    {
        if (!s.ReminderEnabled || !s.ReminderDays.Contains(now.DayOfWeek)) return false;

        var today = DateOnly.FromDateTime(now);
        if (state.ReminderSkippedOn == today) return false;

        var due = today.ToDateTime(s.ReminderTime);
        if (now < due) return false;

        bool hasTarget = s.DailyTargetHours > 0;
        if (hasTarget && hoursToday >= s.DailyTargetHours) return false;

        if (state.LastReminder is { } last && last >= due)
        {
            // Già avvisato oggi: ripete solo se c'è un obiettivo da raggiungere e se è passato l'intervallo.
            if (!hasTarget || s.ReminderRepeatMinutes <= 0) return false;
            if (now - last < TimeSpan.FromMinutes(s.ReminderRepeatMinutes)) return false;
        }
        return true;
    }

    /// <summary>La settimana di cui inviare il report, oppure null se non è dovuto.</summary>
    public static DateOnly? DueReportWeek(AppSettings s, SchedulerState state, DateTime now)
    {
        if (!s.ReportEnabled) return null;

        var weekStart = Fmt.WeekStart(DateOnly.FromDateTime(now));
        var due = weekStart.AddDays(Fmt.MondayIndex(s.ReportDay)).ToDateTime(s.ReportTime);
        // Se il PC era spento all'orario previsto, il report arriva al primo avvio successivo della stessa settimana.
        return now < due || state.LastReportWeek == weekStart ? null : weekStart;
    }
}
