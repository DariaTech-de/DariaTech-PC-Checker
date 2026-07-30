using System.Windows;
using System.Windows.Threading;
using DariaTech.PcDoctor.UI.ViewModels;
using Wpf.Ui.Controls;

namespace DariaTech.PcDoctor.UI.Views;

/// <summary>
/// Eingabemaske für den Zugangs-PIN. Schließt sich selbst, sobald der PIN
/// korrekt war (<see cref="Window.DialogResult"/> = true).
///
/// Der PIN wird bewusst über eine <see cref="System.Windows.Controls.PasswordBox"/>
/// erfasst (verdeckte Eingabe) und nur an das ViewModel zur Prüfung übergeben.
/// </summary>
public partial class PinWindow : FluentWindow
{
    private readonly PinViewModel _viewModel;
    private readonly DispatcherTimer _timer;

    public PinWindow(PinViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // Sekundentakt, damit ein Sperrhinweis mitläuft und sich nach Ablauf löst.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => _viewModel.Refresh();
        _timer.Start();

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += (_, _) => PinBox.Focus();
        Closed += (_, _) =>
        {
            _timer.Stop();
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        };
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Das ViewModel leert den PIN nach jedem Versuch. Eine PasswordBox lässt
        // sich nicht binden – ohne dieses Nachziehen bliebe die Fehleingabe im
        // Feld stehen und würde der nächsten Eingabe vorangestellt. Der zweite
        // Versuch wäre dann zwangsläufig ebenfalls falsch und die Sperre
        // schnappt zu, obwohl der PIN richtig getippt wurde.
        if (e.PropertyName == nameof(PinViewModel.Pin))
        {
            if (_viewModel.Pin.Length == 0 && PinBox.Password.Length > 0)
            {
                PinBox.Clear();
                PinBox.Focus();
            }
            return;
        }

        if (e.PropertyName != nameof(PinViewModel.IsAuthenticated)) return;
        if (!_viewModel.IsAuthenticated) return;

        DialogResult = true;
        Close();
    }

    /// <summary>
    /// Überträgt die Eingabe ins ViewModel. Eine PasswordBox lässt sich aus
    /// Sicherheitsgründen nicht direkt binden.
    /// </summary>
    private void PinBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.PasswordBox box) _viewModel.Pin = box.Password;
    }
}
