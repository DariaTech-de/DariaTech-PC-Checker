using System.Linq;
using DariaTech.PcDoctor.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DariaTech.PcDoctor.Core;

/// <summary>
/// Erzeugt den Kundenbericht direkt als PDF (über QuestPDF, ohne Browser).
/// Layout entspricht dem HTML-Bericht: Petrol-Header mit Score, Übergabe,
/// Ampel-Zusammenfassung, Tabellen je Bereich, Fußzeile mit Firmendaten.
/// </summary>
public static class ReportPdfRenderer
{
    private const string Petrol = "#0E3B34";
    private const string Mint = "#6FE0A8";
    private const string OkColor = "#1A7F37";
    private const string WarnColor = "#9A6700";
    private const string CritColor = "#B3261E";

    static ReportPdfRenderer()
    {
        // Kostenlose Community-Lizenz (für Firmen unter 1 Mio. USD Umsatz zulässig).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static void Render(IReadOnlyList<CheckResult> results, ReportContext? context,
        string computer, DateTime now, string path)
    {
        var score = ReportExporter.HealthScore(results);
        var scoreColor = score >= 80 ? "#1F7A46" : score >= 50 ? "#9A6700" : "#A32B22";
        var critical = results.Where(r => r.Severity == Severity.Critical).ToList();
        var warnings = results.Where(r => r.Severity == Severity.Warning).ToList();

        Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);
            page.DefaultTextStyle(t => t.FontSize(10).FontColor("#1A2433"));

            // Kopf
            page.Header().Background(Petrol).PaddingHorizontal(28).PaddingVertical(16).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("DariaTech IT-Systemhaus").FontColor("#FFFFFF").Bold().FontSize(16);
                    col.Item().Text("PC-Doktor · Kundenbericht").FontColor(Mint).FontSize(9);
                });
                row.ConstantItem(190).Column(col =>
                {
                    col.Item().AlignRight().Text(computer).FontColor("#BFE3D6").FontSize(9);
                    col.Item().AlignRight().Text($"{now:dd.MM.yyyy HH:mm} Uhr").FontColor("#BFE3D6").FontSize(9);
                    col.Item().PaddingTop(4).AlignRight().Background(scoreColor)
                        .PaddingHorizontal(8).PaddingVertical(2)
                        .Text($"Gesundheit {score}/100").FontColor("#FFFFFF").FontSize(9).Bold();
                });
            });

            // Inhalt
            page.Content().PaddingHorizontal(28).PaddingVertical(14).Column(col =>
            {
                if (context is { HasAny: true })
                {
                    col.Item().Text("Übergabe").Bold().FontColor(Petrol).FontSize(12);
                    col.Item().PaddingTop(4).PaddingBottom(6).Column(h =>
                    {
                        HandoverRow(h, "Kunde", context.CustomerName);
                        HandoverRow(h, "Auftrag", context.OrderNumber);
                        HandoverRow(h, "Techniker", context.Technician);
                        HandoverRow(h, "Datum", now.ToString("dd.MM.yyyy"));
                        HandoverRow(h, "Notizen", context.Notes);
                    });
                }

                col.Item().PaddingTop(4).Text("Zusammenfassung").Bold().FontColor(Petrol).FontSize(12);
                if (critical.Count == 0 && warnings.Count == 0)
                    col.Item().PaddingTop(4).Background("#E6F6EA").Padding(8)
                        .Text("Keine Auffälligkeiten gefunden – System sieht gesund aus.").FontColor(OkColor);
                else
                {
                    foreach (var r in critical)
                        col.Item().PaddingTop(4).Background("#FDECEA").Padding(8).Text(Sentence(r)).FontColor(CritColor);
                    foreach (var r in warnings)
                        col.Item().PaddingTop(4).Background("#FFF8E1").Padding(8).Text(Sentence(r)).FontColor(WarnColor);
                }

                foreach (var group in results.GroupBy(r => r.Area))
                {
                    col.Item().PaddingTop(12).BorderBottom(1).BorderColor("#EEF1F5").PaddingBottom(2)
                        .Text(group.Key).Bold().FontColor(Petrol).FontSize(11);

                    foreach (var e in group)
                    {
                        col.Item().PaddingTop(3).Row(row =>
                        {
                            row.ConstantItem(150).Text(e.Label).FontColor("#5A6877");
                            row.RelativeItem().Column(v =>
                            {
                                v.Item().Text(e.Value).FontColor(ColorFor(e.Severity));
                                if (!string.IsNullOrWhiteSpace(e.Detail))
                                    v.Item().Text(e.Detail!).FontColor("#6B7782").FontSize(8.5f);
                                if (e.HasTip)
                                    v.Item().PaddingTop(2).Background("#EEF7F3").Padding(5)
                                        .Text($"💡 {e.Tip}").FontColor("#1A5E4A").FontSize(8.5f);
                            });
                        });
                    }
                }
            });

            // Fuß
            page.Footer().BorderTop(1).BorderColor("#EEF1F5").Background("#F7FAF9")
                .PaddingHorizontal(28).PaddingVertical(8).Column(col =>
            {
                col.Item().Text($"{CompanyInfo.Name} · {CompanyInfo.Street} · {CompanyInfo.City}")
                    .FontColor(Petrol).FontSize(9);
                col.Item().Text($"Telefon: {CompanyInfo.Phone} · E-Mail: {CompanyInfo.Email}")
                    .FontColor(Petrol).FontSize(9);
                col.Item().PaddingTop(2)
                    .Text("Automatisch erstellt mit dem DariaTech PC-Doktor. Werte ohne Gewähr.")
                    .FontColor("#9AA6B0").FontSize(8);
            });
        })).GeneratePdf(path);
    }

    private static void HandoverRow(ColumnDescriptor col, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        col.Item().Row(row =>
        {
            row.ConstantItem(90).Text(label).Bold().FontColor(Petrol).FontSize(9.5f);
            row.RelativeItem().Text(value).FontSize(9.5f);
        });
    }

    private static string Sentence(CheckResult r)
        => string.IsNullOrWhiteSpace(r.Detail) ? $"{r.Area} – {r.Label}: {r.Value}" : r.Detail!;

    private static string ColorFor(Severity s) => s switch
    {
        Severity.Ok => OkColor,
        Severity.Warning => WarnColor,
        Severity.Critical => CritColor,
        _ => "#1A2433"
    };
}
