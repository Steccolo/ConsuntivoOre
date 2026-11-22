namespace ConsuntivoOre.ViewModels;

/// <summary>Le tre schede della finestra principale: Dashboard, Todo, Consuntiva ore.</summary>
public sealed class MainViewModel(DataStore store, JiraService? jira = null)
{
    public DataStore Store { get; } = store;
    public JiraService? Jira { get; } = jira;

    public DashboardViewModel Dashboard { get; } = new(store);
    public TodoViewModel Todo { get; } = new(store, jira);
    public EntryViewModel Entry { get; } = new(store, jira);

    public void Detach()
    {
        Dashboard.Detach();
        Todo.Detach();
        Entry.Detach();
    }

    /// <summary>Dopo un cambio delle impostazioni (ore previste, Jira attivato).</summary>
    public void Reload()
    {
        Dashboard.Refresh();
        Todo.Reload();
        Entry.Reload();
    }

    /// <summary>Si sta scrivendo in una delle schede.</summary>
    public bool HasUnsavedInput => Todo.HasUnsavedInput || Entry.HasUnsavedInput;
}
