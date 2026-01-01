namespace InstanceManager.App.ViewModels;

public sealed class ErrorDialogViewModel
{
    public string Title { get; }
    public string Message { get; }

    public ErrorDialogViewModel(string title, string message)
    {
        Title = title;
        Message = message;
    }
}
