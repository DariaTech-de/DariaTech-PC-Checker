using System.Windows;

namespace DariaTech.PcDoctor.UI.Services;

/// <summary>WPF-Implementierung von <see cref="IDialogService"/>.</summary>
public sealed class DialogService : IDialogService
{
    public bool Confirm(string title, string message)
        => MessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning)
           == MessageBoxResult.OK;

    public void Inform(string title, string message)
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
}
