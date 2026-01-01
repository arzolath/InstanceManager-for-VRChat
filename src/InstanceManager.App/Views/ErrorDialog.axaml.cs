using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace InstanceManager.App.Views;

public partial class ErrorDialog : Window
{
    public ErrorDialog()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
