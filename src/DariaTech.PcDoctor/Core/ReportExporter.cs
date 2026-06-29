using System.Diagnostics;
using System.IO;
using System.Text;
using DariaTech.PcDoctor.Models;

namespace DariaTech.PcDoctor.Core;

/// <summary>
/// Erzeugt einen HTML-Bericht im Stil des PowerShell-Prototyps
/// (Navy-Header, Ampel-Zusammenfassung, Tabellen je Bereich) zum Aushändigen
/// an den Kunden. UI-frei.
/// </summary>
public sealed class ReportExporter
{
    /// <summary>
    /// Schreibt den Bericht in <paramref name="targetFolder"/> und gibt den
    /// vollständigen Dateipfad zurück.
    /// </summary>
    public string Export(
        IReadOnlyList<CheckResult> results,
        ReportContext? context = null,
        string? targetFolder = null,
        DateTime? timestamp = null)
    {
        var folder = targetFolder
            ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        Directory.CreateDirectory(folder);

        var now = timestamp ?? DateTime.Now;
        var computer = Environment.MachineName;
        var file = Path.Combine(folder,
            $"PC-Doktor_{computer}_{now:yyyy-MM-dd_HHmm}.html");

        File.WriteAllText(file, BuildHtml(results, computer, now, context), new UTF8Encoding(false));
        return file;
    }

    /// <summary>
    /// Erzeugt zusätzlich ein PDF, indem der HTML-Bericht über Microsoft Edge
    /// (headless) gedruckt wird – ohne externe Bibliothek. Liefert den PDF-Pfad
    /// oder <c>null</c>, falls Edge nicht gefunden wurde bzw. der Druck scheiterte.
    /// </summary>
    public string? ExportPdf(
        IReadOnlyList<CheckResult> results,
        ReportContext? context = null,
        string? targetFolder = null,
        DateTime? timestamp = null)
    {
        var html = Export(results, context, targetFolder, timestamp);
        var edge = FindEdge();
        if (edge is null) return null;

        var pdf = Path.ChangeExtension(html, ".pdf");
        try
        {
            var psi = new ProcessStartInfo(edge)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("--headless=new");
            psi.ArgumentList.Add("--disable-gpu");
            psi.ArgumentList.Add("--no-pdf-header-footer");
            psi.ArgumentList.Add($"--print-to-pdf={pdf}");
            psi.ArgumentList.Add(new Uri(html).AbsoluteUri);

            using var proc = Process.Start(psi);
            if (proc is null) return null;
            if (!proc.WaitForExit(60_000))
            {
                try { proc.Kill(true); } catch { /* egal */ }
                return null;
            }
            return File.Exists(pdf) ? pdf : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindEdge()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft", "Edge", "Application", "msedge.exe")
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>Gesundheits-Score 0–100 aus den Befunden (für Marketing/Übergabe).</summary>
    public static int HealthScore(IReadOnlyList<CheckResult> results)
    {
        var crit = results.Count(r => r.Severity == Severity.Critical);
        var warn = results.Count(r => r.Severity == Severity.Warning);
        return Math.Clamp(100 - crit * 20 - warn * 7, 0, 100);
    }

    /// <summary>Erzeugt das vollständige HTML-Dokument (öffentlich für Tests).</summary>
    public string BuildHtml(IReadOnlyList<CheckResult> results, string computer, DateTime now,
        ReportContext? context = null)
    {
        var critical = results.Where(r => r.Severity == Severity.Critical).ToList();
        var warnings = results.Where(r => r.Severity == Severity.Warning).ToList();

        var summary = new StringBuilder();
        if (critical.Count == 0 && warnings.Count == 0)
        {
            summary.Append("<div class='ampel ok'>Keine Auffälligkeiten gefunden – System sieht gesund aus.</div>");
        }
        else
        {
            foreach (var r in critical)
                summary.Append($"<div class='ampel crit'>{Enc(Sentence(r))}</div>");
            foreach (var r in warnings)
                summary.Append($"<div class='ampel warn'>{Enc(Sentence(r))}</div>");
        }

        var sections = new StringBuilder();
        foreach (var group in results.GroupBy(r => r.Area))
        {
            sections.Append($"<h2>{Enc(group.Key)}</h2><table>");
            foreach (var e in group)
            {
                sections.Append(
                    $"<tr><td class='label'>{Enc(e.Label)}</td>" +
                    $"<td class='{CssClass(e.Severity)}'>{Enc(e.Value)}</td></tr>");
            }
            sections.Append("</table>");
        }

        var logo = CompanyInfo.LogoSvg(42);
        var score = HealthScore(results);
        var scoreClass = score >= 80 ? "ok" : score >= 50 ? "warn" : "crit";
        var handover = BuildHandover(context, now);

        return $$"""
<!DOCTYPE html><html lang="de"><head><meta charset="utf-8">
<title>PC-Doktor – {{Enc(computer)}}</title>
<style>
  body{font-family:Segoe UI,Arial,sans-serif;background:#f4f6f9;color:#1a2433;margin:0;padding:32px;}
  .wrap{max-width:860px;margin:0 auto;background:#fff;border-radius:10px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.08);}
  header{background:#0E3B34;color:#fff;padding:20px 32px;display:flex;justify-content:space-between;align-items:center;gap:16px;}
  .brand{display:flex;align-items:center;gap:12px;}
  .brand .mark{line-height:0;display:flex;}
  .brand .name{font-size:21px;font-weight:700;letter-spacing:.3px;line-height:1;}
  .brand .tag{font-size:10px;letter-spacing:3px;color:#6FE0A8;text-transform:uppercase;margin-top:3px;}
  .meta{text-align:right;font-size:12.5px;color:#bfe3d6;}
  .meta .doc{font-size:15px;color:#fff;font-weight:600;margin-bottom:2px;}
  .content{padding:24px 32px;}
  .ampel{padding:10px 14px;border-radius:6px;margin:6px 0;font-size:14px;}
  .ampel.ok{background:#e6f6ea;color:#1a7f37;border-left:4px solid #2da44e;}
  .ampel.warn{background:#fff8e1;color:#9a6700;border-left:4px solid #e0b000;}
  .ampel.crit{background:#fdecea;color:#b3261e;border-left:4px solid #d32f2f;}
  h2{font-size:15px;color:#0E3B34;margin:22px 0 6px;border-bottom:2px solid #eef1f5;padding-bottom:4px;}
  table{width:100%;border-collapse:collapse;font-size:13.5px;}
  td{padding:6px 8px;border-bottom:1px solid #f0f2f5;}
  td.label{color:#5a6877;width:230px;}
  td.ok{color:#1a7f37;} td.warn{color:#9a6700;font-weight:600;} td.crit{color:#b3261e;font-weight:600;}
  footer{padding:16px 32px;font-size:12px;border-top:1px solid #eef1f5;background:#f7faf9;}
  footer .pub{color:#0E3B34;font-size:12.5px;}
  footer .disclaimer{color:#9aa6b0;margin-top:6px;}
  .score{display:inline-block;margin-top:6px;padding:3px 10px;border-radius:12px;font-size:12.5px;font-weight:600;}
  .score.ok{background:#1f7a46;color:#eafff2;}
  .score.warn{background:#9a6700;color:#fff7e6;}
  .score.crit{background:#a32b22;color:#ffeceb;}
  .handover{background:#f3f7f6;border:1px solid #e1eae7;border-radius:8px;padding:12px 16px;margin-bottom:8px;}
  .handover td.label{color:#0E3B34;width:120px;font-weight:600;}
</style></head>
<body><div class="wrap">
<header>
  <div class="brand">
    <span class="mark">{{logo}}</span>
    <div><div class="name">DariaTech</div><div class="tag">IT-Systemhaus</div></div>
  </div>
  <div class="meta">
    <div class="doc">PC-Doktor &middot; Kundenbericht</div>
    <div>{{Enc(computer)}} &middot; {{now:dd.MM.yyyy HH:mm}} Uhr</div>
    <div class="score {{scoreClass}}">Gesundheit {{score}}/100</div>
  </div>
</header>
<div class="content">
{{handover}}
<h2>Zusammenfassung</h2>
{{summary}}
{{sections}}
</div>
<footer>
  <div class="pub"><strong>{{Enc(CompanyInfo.Name)}}</strong> &middot; {{Enc(CompanyInfo.Street)}} &middot; {{Enc(CompanyInfo.City)}}</div>
  <div class="pub">Telefon: {{Enc(CompanyInfo.Phone)}} &middot; E-Mail: {{Enc(CompanyInfo.Email)}}</div>
  <div class="disclaimer">Automatisch erstellt mit dem DariaTech PC-Doktor. Werte ohne Gewähr.</div>
</footer>
</div></body></html>
""";
    }

    private static string Sentence(CheckResult r)
        => string.IsNullOrWhiteSpace(r.Detail)
            ? $"{r.Area} – {r.Label}: {r.Value}"
            : r.Detail!;

    private static string CssClass(Severity s) => s switch
    {
        Severity.Ok => "ok",
        Severity.Warning => "warn",
        Severity.Critical => "crit",
        _ => "info"
    };

    private static string BuildHandover(ReportContext? c, DateTime now)
    {
        if (c is null || !c.HasAny) return string.Empty;

        var sb = new StringBuilder("<h2>Übergabe</h2><div class='handover'><table>");
        void Row(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                sb.Append($"<tr><td class='label'>{Enc(label)}</td><td>{Enc(value)}</td></tr>");
        }
        Row("Kunde", c.CustomerName);
        Row("Auftrag", c.OrderNumber);
        Row("Techniker", c.Technician);
        Row("Datum", now.ToString("dd.MM.yyyy"));
        Row("Notizen", c.Notes);
        sb.Append("</table></div>");
        return sb.ToString();
    }

    // Wie der PowerShell-Prototyp: nur &, &lt;, &gt; ersetzen (Reihenfolge: & zuerst).
    // So bleiben Umlaute und Sonderzeichen als lesbares UTF-8 im Bericht erhalten.
    private static string Enc(string? text)
        => (text ?? string.Empty)
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
}
