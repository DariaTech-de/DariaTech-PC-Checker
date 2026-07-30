using System.Windows;
using System.Windows.Threading;
using DariaTech.PcDoctor.Core.Security;
using DariaTech.PcDoctor.UI.ViewModels;
using Wpf.Ui.Controls;

namespace DariaTech.PcDoctor.UI.Views;

public partial class MainWindow : FluentWindow
{
    private readonly PinSession? _session;
    private readonly IServiceProvider? _services;
    private readonly DispatcherTimer? _idleTimer;
    private bool _relockInProgress;

    public MainWindow(MainViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>
    /// Fassung mit Zugangsschutz: sperrt die Anwendung nach 30 Minuten ohne
    /// Bedienung wieder, damit ein unbeaufsichtigtes Notebook beim Kunden nicht
    /// offen zugänglich bleibt.
    /// </summary>
    public MainWindow(MainViewModel viewModel, PinSession session, IServiceProvider services)
        : this(viewModel)
    {
        // Zeitsperre NUR aktivieren, wenn dieser Build überhaupt einen PIN hat.
        // Sonst erschiene eine Abfrage, die sich nicht entsperren lässt – der
        // Techniker wäre ausgeschlossen.
        if (!PinSecret.IsConfigured) return;

        _session = session;
        _services = services;
        _session.Unlock(DateTime.UtcNow);

        // Jede Bedienung setzt die Untätigkeitsuhr zurück.
        PreviewMouseDown += (_, _) => RegisterActivity();
        PreviewKeyDown += (_, _) => RegisterActivity();
        PreviewMouseWheel += (_, _) => RegisterActivity();

        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _idleTimer.Tick += (_, _) => CheckIdleTimeout();
        _idleTimer.Start();

        Closed += (_, _) => _idleTimer.Stop();
    }

    private void RegisterActivity() => _session?.RegisterActivity(DateTime.UtcNow);

    /// <summary>
    /// True, solange ein Vorgang läuft, der nicht unterbrochen werden darf.
    /// Ein Stresstest, ein Klonvorgang oder eine laufende Reparatur brauchen
    /// keine Mausbewegung – die Zeitsperre würde sonst mitten in einen Eingriff
    /// grätschen. Bricht der Techniker die PIN-Abfrage dann ab, beendet sich die
    /// App und ein halb erledigter Systemeingriff bleibt zurück.
    /// </summary>
    private bool IsWorkInProgress() => DataContext is MainViewModel vm
        && (vm.IsScanning || vm.IsFixRunning
            || vm.Symptoms.IsFixRunning
            || vm.Gaming.IsStressRunning
            || vm.Clone.IsCloning);

    /// <summary>
    /// Prüft die Untätigkeitsdauer. Bei Überschreitung wird das Fenster
    /// ausgeblendet und der PIN erneut verlangt; bei Abbruch schließt die App.
    /// </summary>
    private void CheckIdleTimeout()
    {
        if (_session is null || _services is null || _relockInProgress) return;

        // Läuft ein Vorgang, gilt das als Aktivität: Uhr weiterschieben, nicht sperren.
        if (IsWorkInProgress())
        {
            _session.RegisterActivity(DateTime.UtcNow);
            return;
        }

        if (!_session.LockIfIdleTimeoutReached(DateTime.UtcNow)) return;

        _relockInProgress = true;
        try
        {
            Hide();
            if (App.RequestPinAgain(_services))
            {
                _session.Unlock(DateTime.UtcNow);
                Show();
                Activate();
            }
            else
            {
                Application.Current.Shutdown();
            }
        }
        finally
        {
            _relockInProgress = false;
        }
    }

    /// <summary>
    /// Startet die Live-Überwachung automatisch, sobald der Gaming-Tab geöffnet wird,
    /// damit Tachos und Diagramm sofort Werte zeigen (statt erst nach Klick).
    /// </summary>
    private void OnTabChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, MainTabs)) return;
        if (DataContext is MainViewModel vm
            && MainTabs.SelectedItem is System.Windows.Controls.TabItem tab
            && tab.Header as string == "Gaming & Stresstest")
        {
            _ = vm.Gaming.EnsureMonitoringAsync();
        }
    }

    /// <summary>
    /// Schließt das Detail-Popup, wenn der Nutzer auf den abgedunkelten Bereich
    /// neben dem Popup klickt (nicht auf das Popup selbst).
    /// </summary>
    private void OnAreaPopupBackdropMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, sender) && DataContext is MainViewModel vm)
            vm.CloseAreaCommand.Execute(null);
    }
}
