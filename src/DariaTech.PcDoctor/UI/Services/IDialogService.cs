namespace DariaTech.PcDoctor.UI.Services;

/// <summary>
/// Abstraktion für Nutzerdialoge, damit ViewModels testbar/UI-frei bleiben.
/// </summary>
public interface IDialogService
{
    /// <summary>Bestätigungsdialog vor systemverändernden Aktionen.</summary>
    bool Confirm(string title, string message);

    /// <summary>Reine Informationsmeldung.</summary>
    void Inform(string title, string message);
}
