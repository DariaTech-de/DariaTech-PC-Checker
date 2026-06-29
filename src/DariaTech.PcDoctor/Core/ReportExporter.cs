using System.IO;
using System.Net;
using System.Text;

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

        File.WriteAllText(file, BuildHtml(results, computer, now), new UTF8Encoding(false));
        return file;
    }

    /// <summary>Erzeugt das vollständige HTML-Dokument (öffentlich für Tests).</summary>
    public string BuildHtml(IReadOnlyList<CheckResult> results, string computer, DateTime now)
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

        return $$"""
<!DOCTYPE html><html lang="de"><head><meta charset="utf-8">
<title>PC-Doktor – {{Enc(computer)}}</title>
<style>
  body{font-family:Segoe UI,Arial,sans-serif;background:#f4f6f9;color:#1a2433;margin:0;padding:32px;}
  .wrap{max-width:860px;margin:0 auto;background:#fff;border-radius:10px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.08);}
  header{background:#0d1f3c;color:#fff;padding:24px 32px;}
  header h1{margin:0;font-size:20px;}
  header .sub{color:#7fa8e6;font-size:13px;margin-top:4px;}
  .content{padding:24px 32px;}
  .ampel{padding:10px 14px;border-radius:6px;margin:6px 0;font-size:14px;}
  .ampel.ok{background:#e6f6ea;color:#1a7f37;border-left:4px solid #2da44e;}
  .ampel.warn{background:#fff8e1;color:#9a6700;border-left:4px solid #e0b000;}
  .ampel.crit{background:#fdecea;color:#b3261e;border-left:4px solid #d32f2f;}
  h2{font-size:15px;color:#0d1f3c;margin:22px 0 6px;border-bottom:2px solid #eef1f5;padding-bottom:4px;}
  table{width:100%;border-collapse:collapse;font-size:13.5px;}
  td{padding:6px 8px;border-bottom:1px solid #f0f2f5;}
  td.label{color:#5a6877;width:230px;}
  td.ok{color:#1a7f37;} td.warn{color:#9a6700;font-weight:600;} td.crit{color:#b3261e;font-weight:600;}
  footer{padding:16px 32px;color:#8a96a3;font-size:12px;border-top:1px solid #eef1f5;}
</style></head>
<body><div class="wrap">
<header><h1>PC-Doktor</h1>
<div class="sub">{{Enc(computer)}} &middot; erstellt am {{now:dd.MM.yyyy HH:mm}} &middot; DariaTech IT-Systemhaus</div></header>
<div class="content">
<h2>Zusammenfassung</h2>
{{summary}}
{{sections}}
</div>
<footer>Automatisch erstellt mit dem DariaTech PC-Doktor. Werte ohne Gewähr.</footer>
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

    private static string Enc(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);
}
