using Avalonia.Controls;

namespace ConsuntivoOre.Views;

/// <summary>Ricorda le larghezze delle colonne di una tabella tra un'apertura e l'altra della finestra.</summary>
public static class ColumnLayout
{
    /// <summary>
    /// Applica le larghezze salvate per <paramref name="key"/> e le salva alla chiusura della finestra.
    /// Le colonne che riempiono lo spazio rimasto (larghezza "*") restano così: si salvano ma non si reimpostano.
    /// </summary>
    public static void Remember(DataGrid grid, string key, DataStore store, Window window)
    {
        if (store.Settings.ColumnWidths.TryGetValue(key, out var saved) && saved.Count == grid.Columns.Count)
        {
            for (int i = 0; i < saved.Count; i++)
                if (!grid.Columns[i].Width.IsStar) grid.Columns[i].Width = new DataGridLength(saved[i]);
        }

        window.Closing += (_, _) =>
        {
            // Una colonna nascosta (es. Jira disattivato) ha larghezza 0: si conserva quella impostata.
            var widths = grid.Columns
                .Select(c => Math.Round(c.IsVisible || !c.Width.IsAbsolute ? c.ActualWidth : c.Width.Value))
                .ToList();
            // Finestra mai disegnata (o colonne non ancora misurate): non c'è niente da ricordare.
            if (widths.Count == 0 || widths.Any(w => !double.IsFinite(w) || w <= 0)) return;
            if (store.Settings.ColumnWidths.TryGetValue(key, out var old) && old.SequenceEqual(widths)) return;

            store.Settings.ColumnWidths[key] = widths;
            try
            {
                store.SaveSettings();
            }
            catch (Exception ex) when (DataStore.IsFileError(ex))
            {
                // Non è grave: alla prossima apertura tornano le larghezze predefinite.
                Log.Error(ex);
            }
        };
    }
}
