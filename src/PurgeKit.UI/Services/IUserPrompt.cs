namespace PurgeKit.UI.Services;

/// <summary>Modal questions and notices shown to the user.</summary>
public interface IUserPrompt
{
    /// <summary>Asks a yes/no question; true when the user agrees.</summary>
    bool Confirm(string title, string message);

    /// <summary>Shows an informational notice.</summary>
    void Inform(string title, string message);

    /// <summary>Shows an error with what went wrong and what to do next.</summary>
    void ShowError(string title, string message);
}
