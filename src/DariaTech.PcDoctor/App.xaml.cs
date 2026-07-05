using System.IO;
using System.Windows;
using DariaTech.PcDoctor.Checks;
using DariaTech.PcDoctor.Core;
using DariaTech.PcDoctor.Core.Clone;
using DariaTech.PcDoctor.Core.StressTest;
using DariaTech.PcDoctor.Fixes;
using DariaTech.PcDoctor.Infrastructure;
using DariaTech.PcDoctor.Models;
using DariaTech.PcDoctor.UI.Services;
using DariaTech.PcDoctor.UI.ViewModels;
using DariaTech.PcDoctor.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DariaTech.PcDoctor;

/// <summary>
/// Einstiegspunkt: richtet Hosting, DI und Serilog ein und zeigt das
/// Hauptfenster. Alle Checks und Fixes werden hier registriert und vom
/// DiagnosticEngine bzw. der UI über IEnumerable&lt;…&gt; eingesammelt.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DariaTech", "PC-Doktor", "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDir, "pc-doktor-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(ConfigureServices)
            .Build();

        await _host.StartAsync();

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Optionen
        services.AddSingleton<ScanOptions>();

        // Checks (Reihenfolge = Reihenfolge im Dashboard)
        services.AddSingleton<ICheck, SystemInfoCheck>();
        services.AddSingleton<ICheck, CpuMemoryCheck>();
        services.AddSingleton<ICheck, DiskSpaceCheck>();
        services.AddSingleton<ICheck, SmartHealthCheck>();
        services.AddSingleton<ICheck, StorageDetailCheck>();
        services.AddSingleton<ICheck, BatteryCheck>();
        services.AddSingleton<ICheck, SecurityCheck>();
        services.AddSingleton<ICheck, BackupStatusCheck>();
        services.AddSingleton<ICheck, WindowsUpdateCheck>();
        services.AddSingleton<ICheck, StartupCheck>();
        services.AddSingleton<ICheck, DriverDeviceCheck>();
        services.AddSingleton<ICheck, NetworkCheck>();
        services.AddSingleton<ICheck, EventLogCheck>();

        // Fixes (bereichsbezogen; DisableStartupItemFix wird je Eintrag vom Check erzeugt)
        services.AddSingleton<IFixAction, ClearTempFilesFix>();
        services.AddSingleton<IFixAction, ClearAppCacheFix>();
        services.AddSingleton<IFixAction, SystemFileRepairFix>();
        services.AddSingleton<IFixAction, FlushDnsFix>();
        services.AddSingleton<IFixAction, DefenderQuickScanFix>();
        services.AddSingleton<IFixAction, WindowsUpdateRepairFix>();
        services.AddSingleton<IFixAction, CheckDiskFix>();
        services.AddSingleton<IFixAction, WinsockResetFix>();
        services.AddSingleton<IFixAction, BatteryReportFix>();
        services.AddSingleton<IFixAction, SpeedTestFix>();
        services.AddSingleton<IFixAction, NetworkResetFix>();
        services.AddSingleton<IFixAction, PrinterSpoolerResetFix>();
        services.AddSingleton<IFixAction, RestartExplorerFix>();
        services.AddSingleton<IFixAction, PowerPlanHighPerformanceFix>();
        services.AddSingleton<IFixAction, GroupPolicyUpdateFix>();

        // Core-Dienste
        services.AddSingleton<DiagnosticEngine>();
        services.AddSingleton<RestorePointService>();
        services.AddSingleton<RepairService>();
        services.AddSingleton<ReportExporter>();

        // Sensorik + Stresstest (Gaming-PCs)
        services.AddSingleton<ISensorService, LibreHardwareSensorService>();
        services.AddSingleton<StressTestService>();

        // Verlauf
        services.AddSingleton<IHistoryStore, JsonHistoryStore>();

        // Klonen (Datenträger 1:1)
        services.AddSingleton<IPhysicalDiskService, WmiPhysicalDiskService>();
        services.AddSingleton<DiskCloneService>();

        // UI
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<GamingViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<CloneViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
