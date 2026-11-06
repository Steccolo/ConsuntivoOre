namespace ConsuntivoOre.ViewModels;

/// <summary>Le due schede della finestra principale.</summary>
public sealed class MainViewModel(DataStore store)
{
    public DataStore Store { get; } = store;

    public TodoViewModel Todo { get; } = new(store);
    public EntryViewModel Entry { get; } = new(store);

    public void Detach()
    {
        Todo.Detach();
        Entry.Detach();
    }

    /// <summary>Si sta scrivendo in una delle due schede.</summary>
    public bool HasUnsavedInput => Todo.HasUnsavedInput || Entry.HasUnsavedInput;
}
