using System.Reflection;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Core.Symptoms;
using DariaTech.PcDoctor.Models;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

public class SymptomCatalogTests
{
    [Fact]
    public void All_IsNotEmpty()
        => Assert.NotEmpty(SymptomCatalog.All);

    [Fact]
    public void All_HaveUniqueIds()
    {
        var ids = SymptomCatalog.All.Select(s => s.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void All_HaveFilledTexts()
    {
        foreach (var s in SymptomCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Id), $"Id fehlt bei {s.Title}");
            Assert.False(string.IsNullOrWhiteSpace(s.Title), $"Titel fehlt bei {s.Id}");
            Assert.False(string.IsNullOrWhiteSpace(s.Question), $"Kundenformulierung fehlt bei {s.Id}");
            Assert.False(string.IsNullOrWhiteSpace(s.Advice), $"Vorgehenshinweis fehlt bei {s.Id}");
            Assert.NotEmpty(s.CheckAreas);
        }
    }

    [Fact]
    public void All_FixTypes_ImplementIFixAction_AndAreDistinct()
    {
        foreach (var s in SymptomCatalog.All)
        {
            Assert.All(s.FixTypes, t =>
                Assert.True(typeof(IFixAction).IsAssignableFrom(t),
                    $"{t.Name} in Symptom {s.Id} ist keine Reparatur (IFixAction)."));

            Assert.Equal(s.FixTypes.Count, s.FixTypes.Distinct().Count());
        }
    }

    /// <summary>
    /// Wichtigster Test: Jeder im Katalog genannte Prüfbereich muss exakt dem
    /// <see cref="ICheck.Area"/> einer real existierenden Prüfung entsprechen.
    /// Ein Tippfehler würde sonst dazu führen, dass der Assistent stillschweigend
    /// nichts prüft.
    /// </summary>
    [Fact]
    public void All_CheckAreas_MatchExistingChecks()
    {
        var known = KnownCheckAreas();

        foreach (var symptom in SymptomCatalog.All)
        foreach (var area in symptom.CheckAreas)
            Assert.True(known.Contains(area),
                $"Symptom „{symptom.Id}“ verweist auf den unbekannten Bereich „{area}“. " +
                $"Bekannt sind: {string.Join(" | ", known.OrderBy(a => a))}");
    }

    [Fact]
    public void All_CheckAreas_AreDistinctPerSymptom()
    {
        foreach (var s in SymptomCatalog.All)
            Assert.Equal(s.CheckAreas.Count,
                s.CheckAreas.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("search")]
    [InlineData("explorer-crash")]
    [InlineData("slow")]
    [InlineData("network")]
    [InlineData("printer")]
    [InlineData("crashes")]
    public void ById_FindsTheCoreSymptoms(string id)
        => Assert.NotNull(SymptomCatalog.ById(id));

    [Fact]
    public void ById_UnknownOrNull_ReturnsNull()
    {
        Assert.Null(SymptomCatalog.ById("gibt-es-nicht"));
        Assert.Null(SymptomCatalog.ById(null));
    }

    /// <summary>
    /// Liest die Area-Werte aller vorhandenen <see cref="ICheck"/>-Implementierungen.
    /// Die Konstruktoren der Prüfungen setzen nur Felder (keine I/O), das Erzeugen
    /// ist daher im Test unbedenklich.
    /// </summary>
    private static HashSet<string> KnownCheckAreas()
    {
        var areas = new HashSet<string>(StringComparer.Ordinal);

        var checkTypes = LoadableTypes(typeof(ICheck).Assembly)
            .Where(t => typeof(ICheck).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

        foreach (var type in checkTypes)
        {
            var instance = TryCreate(type);
            if (instance is ICheck check) areas.Add(check.Area);
        }

        Assert.NotEmpty(areas);   // Absicherung: Reflexion hat wirklich Prüfungen gefunden
        return areas;
    }

    /// <summary>
    /// Alle ladbaren Typen einer Assembly. Bei WPF-Assemblies kann ein einzelner
    /// Typ nicht ladbar sein – dann mit den übrigen weiterarbeiten statt scheitern.
    /// </summary>
    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null).Cast<Type>();
        }
    }

    private static object? TryCreate(Type type)
    {
        foreach (var ctor in type.GetConstructors().OrderBy(c => c.GetParameters().Length))
        {
            try
            {
                var args = ctor.GetParameters().Select(ResolveArgument).ToArray();
                return ctor.Invoke(args);
            }
            catch { /* nächsten Konstruktor versuchen */ }
        }
        return null;
    }

    /// <summary>Erzeugt Ersatzobjekte für die wenigen Konstruktor-Abhängigkeiten der Prüfungen.</summary>
    private static object? ResolveArgument(ParameterInfo p)
    {
        if (p.ParameterType == typeof(ISensorService)) return new StubSensorService();
        if (p.ParameterType == typeof(ScanOptions)) return new ScanOptions();
        return null;
    }

    private sealed class StubSensorService : ISensorService
    {
        public bool IsAvailable => false;
        public IReadOnlyList<SensorReading> Read() => Array.Empty<SensorReading>();
        public void Dispose() { }
    }
}
