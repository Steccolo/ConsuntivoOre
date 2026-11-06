using ConsuntivoOre.Core;

namespace ConsuntivoOre.Tests;

public class SchedulerTests
{
    // Lunedì 2 novembre 2026.
    private static readonly DateTime Monday = new(2026, 11, 2);

    private static DateTime At(int hour, int minute = 0) => Monday.AddHours(hour).AddMinutes(minute);

    [Fact]
    public void Reminder_NotDueBeforeTime() =>
        Assert.False(Scheduler.IsReminderDue(new AppSettings(), new SchedulerState(), At(17, 29), 0));

    [Fact]
    public void Reminder_DueAtTime() =>
        Assert.True(Scheduler.IsReminderDue(new AppSettings(), new SchedulerState(), At(17, 30), 0));

    [Fact]
    public void Reminder_NotDueOnOtherDays() =>
        Assert.False(Scheduler.IsReminderDue(new AppSettings(), new SchedulerState(), At(18).AddDays(5), 0));

    [Fact]
    public void Reminder_NotDueWhenTargetReached() =>
        Assert.False(Scheduler.IsReminderDue(new AppSettings(), new SchedulerState(), At(18), 8));

    [Fact]
    public void Reminder_RepeatsOnlyAfterInterval()
    {
        var state = new SchedulerState { LastReminder = At(17, 30) };
        Assert.False(Scheduler.IsReminderDue(new AppSettings(), state, At(17, 59), 0));
        Assert.True(Scheduler.IsReminderDue(new AppSettings(), state, At(18), 0));
    }

    [Fact]
    public void Reminder_DoesNotRepeatWithoutTarget()
    {
        var settings = new AppSettings { DailyTargetHours = 0 };
        Assert.True(Scheduler.IsReminderDue(settings, new SchedulerState(), At(18), 0));
        var state = new SchedulerState { LastReminder = At(17, 30) };
        Assert.False(Scheduler.IsReminderDue(settings, state, At(23), 0));
    }

    [Fact]
    public void Reminder_DoesNotRepeatWhenIntervalIsZero()
    {
        var settings = new AppSettings { ReminderRepeatMinutes = 0 };
        var state = new SchedulerState { LastReminder = At(17, 30) };
        Assert.False(Scheduler.IsReminderDue(settings, state, At(23), 0));
    }

    [Fact]
    public void Reminder_SkippedOnlyForThatDay()
    {
        var state = new SchedulerState { ReminderSkippedOn = DateOnly.FromDateTime(Monday) };
        Assert.False(Scheduler.IsReminderDue(new AppSettings(), state, At(18), 0));
        Assert.True(Scheduler.IsReminderDue(new AppSettings(), state, At(18).AddDays(1), 0));
    }

    [Fact]
    public void Report_DueFromScheduledTimeUntilSent()
    {
        var friday = Monday.AddDays(4);
        var weekStart = DateOnly.FromDateTime(Monday);
        Assert.Null(Scheduler.DueReportWeek(new AppSettings(), new SchedulerState(), friday.AddHours(17)));
        Assert.Equal(weekStart, Scheduler.DueReportWeek(new AppSettings(), new SchedulerState(), friday.AddHours(18)));
        // PC spento il venerdì: arriva comunque nel weekend.
        Assert.Equal(weekStart, Scheduler.DueReportWeek(new AppSettings(), new SchedulerState(), friday.AddDays(2)));
        var sent = new SchedulerState { LastReportWeek = weekStart };
        Assert.Null(Scheduler.DueReportWeek(new AppSettings(), sent, friday.AddHours(18)));
    }

    [Fact]
    public void SnoozedReminder_WaitsThenComesBackOnce()
    {
        // Senza obiettivo il promemoria normalmente non si ripete: il rinvio lo fa tornare comunque una volta.
        var settings = new AppSettings { DailyTargetHours = 0 };
        var state = new SchedulerState { LastReminder = At(17, 30), ReminderSnoozedUntil = At(18) };
        Assert.False(Scheduler.IsReminderDue(settings, state, At(17, 59), 0));
        Assert.True(Scheduler.IsReminderDue(settings, state, At(18), 0));

        state.LastReminder = At(18);
        Assert.False(Scheduler.IsReminderDue(settings, state, At(20), 0));
    }

    [Fact]
    public void SnoozedReminder_DelaysTheRegularRepeat()
    {
        var state = new SchedulerState { LastReminder = At(17, 30), ReminderSnoozedUntil = At(19) };
        Assert.False(Scheduler.IsReminderDue(new AppSettings(), state, At(18, 30), 0));
        Assert.True(Scheduler.IsReminderDue(new AppSettings(), state, At(19), 0));
    }

    [Fact]
    public void Snooze_FromAnotherDay_IsIgnored()
    {
        var state = new SchedulerState { ReminderSnoozedUntil = At(23).AddDays(-1) };
        Assert.True(Scheduler.IsReminderDue(new AppSettings(), state, At(17, 30), 0));
    }

    [Fact]
    public void Snooze_DoesNotBypassReachedTarget()
    {
        var state = new SchedulerState { LastReminder = At(17, 30), ReminderSnoozedUntil = At(18) };
        Assert.False(Scheduler.IsReminderDue(new AppSettings(), state, At(18), 8));
    }
}
