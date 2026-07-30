using DariaTech.PcDoctor.Fixes;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

/// <summary>
/// Der Autostart-Fix löscht einen Wert aus der Registry. Der Schlüsselpfad
/// stammt aus <c>Win32_StartupCommand</c>, kommt also von außen. Ohne Schranke
/// würde die Aktion im schlechtesten Fall an einer völlig anderen Stelle der
/// Registry etwas entfernen – deshalb ist diese Regel abgesichert.
/// </summary>
public class StartupItemSafetyTests
{
    [Theory]
    [InlineData(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run")]
    [InlineData(@"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce")]
    [InlineData(@"Software\Microsoft\Windows\CurrentVersion\run")]
    [InlineData(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Run")]
    [InlineData(@"S-1-5-21-1\SOFTWARE\Microsoft\Windows\CurrentVersion\Run")]
    [InlineData(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run\")]
    public void AutostartKeys_AreAllowed(string subPath)
        => Assert.True(DisableStartupItemFix.IsAllowedRunKey(subPath));

    [Theory]
    [InlineData(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run\DariaTech_Deaktiviert")]
    [InlineData(@"SYSTEM\CurrentControlSet\Services")]
    [InlineData(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon")]
    [InlineData(@"SOFTWARE\Classes")]
    [InlineData(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer")]
    [InlineData("")]
    [InlineData(null)]
    public void EverythingElse_IsRefused(string? subPath)
        => Assert.False(DisableStartupItemFix.IsAllowedRunKey(subPath));
}
