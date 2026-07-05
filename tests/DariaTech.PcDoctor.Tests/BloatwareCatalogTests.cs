using DariaTech.PcDoctor.Core;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

public class BloatwareCatalogTests
{
    [Fact]
    public void Packages_AreNonEmptyAndUnique()
    {
        Assert.NotEmpty(BloatwareCatalog.Packages);
        Assert.All(BloatwareCatalog.Packages, p => Assert.False(string.IsNullOrWhiteSpace(p)));
        Assert.Equal(BloatwareCatalog.Packages.Count, BloatwareCatalog.Packages.Distinct().Count());
    }

    [Fact]
    public void Packages_ContainNoProtectedPackage()
    {
        // Schutznetz: der Entfern-Katalog darf niemals ein system-/sicherheits-
        // relevantes Paket enthalten.
        foreach (var protectedPkg in BloatwareCatalog.NeverRemove)
            Assert.DoesNotContain(protectedPkg, BloatwareCatalog.Packages);
    }
}
