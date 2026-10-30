namespace PurgeKit.UI.ViewModels;

/// <summary>A labelled choice in a filter drop-down.</summary>
/// <param name="Label">Text shown to the user.</param>
/// <param name="Value">Value the filter uses.</param>
public sealed record FilterOption<T>(string Label, T Value)
{
    public override string ToString() => Label;
}
