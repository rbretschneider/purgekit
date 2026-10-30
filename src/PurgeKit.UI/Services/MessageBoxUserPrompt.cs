using System.Windows;

namespace PurgeKit.UI.Services;

/// <summary>Prompts with standard Windows message boxes, matching the rest of PurgeKit.</summary>
public sealed class MessageBoxUserPrompt : IUserPrompt
{
    /// <inheritdoc />
    public bool Confirm(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    /// <inheritdoc />
    public void Inform(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    /// <inheritdoc />
    public void ShowError(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
}
