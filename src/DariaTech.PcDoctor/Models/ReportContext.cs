namespace DariaTech.PcDoctor.Models;

/// <summary>
/// Optionale Kopf-/Übergabedaten für den Kundenbericht (vom Techniker vor Ort
/// ausgefüllt). Alle Felder optional – leere Felder werden im Bericht weggelassen.
/// </summary>
public sealed class ReportContext
{
    public string? CustomerName { get; init; }
    public string? OrderNumber { get; init; }
    public string? Technician { get; init; }
    public string? Notes { get; init; }

    public bool HasAny =>
        !string.IsNullOrWhiteSpace(CustomerName) ||
        !string.IsNullOrWhiteSpace(OrderNumber) ||
        !string.IsNullOrWhiteSpace(Technician) ||
        !string.IsNullOrWhiteSpace(Notes);
}
