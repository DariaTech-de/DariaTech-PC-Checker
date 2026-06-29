using DariaTech.PcDoctor.UI.ViewModels;
using Wpf.Ui.Controls;

namespace DariaTech.PcDoctor.UI.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
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
}
