using DariaTech.PcDoctor.Core;
using Xunit;

namespace DariaTech.PcDoctor.Tests;

/// <summary>
/// Die Zuordnung bestimmt, ob der Symptom-Assistent beim Symptom „kein Ton“
/// tatsächlich die Audiogeräte auswertet. Greift sie daneben, prüft der
/// Assistent stillschweigend die falsche Kategorie.
/// </summary>
public class DeviceCategoriesTests
{
    [Theory]
    [InlineData("MEDIA", DeviceCategories.Audio)]
    [InlineData("AudioEndpoint", DeviceCategories.Audio)]
    [InlineData("Bluetooth", DeviceCategories.Bluetooth)]
    [InlineData("Camera", DeviceCategories.Camera)]
    [InlineData("Image", DeviceCategories.Camera)]
    [InlineData("USB", DeviceCategories.Usb)]
    [InlineData("Display", DeviceCategories.Display)]
    [InlineData("Monitor", DeviceCategories.Display)]
    [InlineData("Net", DeviceCategories.Network)]
    [InlineData("Printer", DeviceCategories.Printer)]
    [InlineData("HIDClass", DeviceCategories.Input)]
    [InlineData("Keyboard", DeviceCategories.Input)]
    [InlineData("Mouse", DeviceCategories.Input)]
    [InlineData("Battery", DeviceCategories.Power)]
    public void Classify_MapsWindowsDeviceClasses(string pnpClass, string expected)
        => Assert.Equal(expected, DeviceCategories.Classify(pnpClass));

    [Fact]
    public void Classify_IsCaseInsensitive()
        => Assert.Equal(DeviceCategories.Bluetooth, DeviceCategories.Classify("bluetooth"));

    [Theory]
    [InlineData("Intel(R) Wireless Bluetooth(R)", DeviceCategories.Bluetooth)]
    [InlineData("Integrierte Webcam", DeviceCategories.Camera)]
    [InlineData("Realtek High Definition Audio", DeviceCategories.Audio)]
    [InlineData("NVIDIA GeForce RTX 5090", DeviceCategories.Display)]
    [InlineData("USB-Massenspeichergerät", DeviceCategories.Usb)]
    public void Classify_FallsBackToDeviceName_WhenClassIsMissing(string name, string expected)
        => Assert.Equal(expected, DeviceCategories.Classify(pnpClass: null, name: name));

    [Theory]
    [InlineData("System", "Hochpräziser Ereigniszeitgeber")]
    [InlineData("SoftwareComponent", "Intel Management Engine")]
    [InlineData(null, null)]
    [InlineData("", "")]
    public void Classify_IgnoresIrrelevantDevices(string? pnpClass, string? name)
        => Assert.Null(DeviceCategories.Classify(pnpClass, name));

    [Fact]
    public void All_ContainsEveryCategoryUsedByTheClassifier()
    {
        // Absicherung: Was der Klassifizierer liefern kann, muss auch in All stehen.
        string?[] samples =
        {
            DeviceCategories.Classify("MEDIA"), DeviceCategories.Classify("Bluetooth"),
            DeviceCategories.Classify("Camera"), DeviceCategories.Classify("USB"),
            DeviceCategories.Classify("Display"), DeviceCategories.Classify("Net"),
            DeviceCategories.Classify("Printer"), DeviceCategories.Classify("HIDClass"),
            DeviceCategories.Classify("Battery")
        };

        Assert.All(samples, category =>
        {
            Assert.NotNull(category);
            Assert.Contains(category!, DeviceCategories.All);
        });
    }

    [Theory]
    [InlineData(10)]
    [InlineData(22)]
    [InlineData(28)]
    [InlineData(43)]
    [InlineData(999)]
    public void CodeMeaning_AlwaysExplainsSomething(int code)
        => Assert.False(string.IsNullOrWhiteSpace(DeviceCategories.CodeMeaning(code)));

    [Fact]
    public void CodeMeaning_ForDisabledDevice_MentionsDeactivation()
        => Assert.Contains("DEAKTIVIERT", DeviceCategories.CodeMeaning(22));

    [Fact]
    public void CodeMeaning_ForHardwareFault_MentionsDefect()
        => Assert.Contains("Defekt", DeviceCategories.CodeMeaning(43));

    [Theory]
    [InlineData(22, true)]
    [InlineData(0, false)]
    [InlineData(10, false)]
    [InlineData(43, false)]
    public void IsDisabled_OnlyMatchesCode22(int code, bool expected)
        => Assert.Equal(expected, DeviceCategories.IsDisabled(code));
}
