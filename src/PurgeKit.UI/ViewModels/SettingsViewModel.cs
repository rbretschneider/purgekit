using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PurgeKit.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    [ObservableProperty]
    private string _theme;

    [ObservableProperty]
    private bool _useRecycleBin;

    [ObservableProperty]
    private int _minFileAgeDays;

    [ObservableProperty]
    private int _minFileSizeMb;

    [ObservableProperty]
    private ObservableCollection<string> _excludedPaths;

    [ObservableProperty]
    private string _newExcludedPath = "";

    public SettingsViewModel(MainViewModel main)
    {
        _main = main;
        var s = App.Settings;
        _theme = s.Theme;
        _useRecycleBin = s.UseRecycleBin;
        _minFileAgeDays = s.MinFileAgeDays;
        _minFileSizeMb = (int)(s.MinFileSizeBytes / (1024 * 1024));
        _excludedPaths = new ObservableCollection<string>(s.ExcludedPaths);
    }

    partial void OnThemeChanged(string value)
    {
        App.Settings.Theme = value;
        App.ApplyTheme(value);
        Save();
    }

    partial void OnUseRecycleBinChanged(bool value)
    {
        App.Settings.UseRecycleBin = value;
        Save();
    }

    partial void OnMinFileAgeDaysChanged(int value)
    {
        App.Settings.MinFileAgeDays = value;
        Save();
    }

    partial void OnMinFileSizeMbChanged(int value)
    {
        App.Settings.MinFileSizeBytes = (long)value * 1024 * 1024;
        Save();
    }

    [RelayCommand]
    private void AddExcludedPath()
    {
        if (string.IsNullOrWhiteSpace(NewExcludedPath)) return;
        ExcludedPaths.Add(NewExcludedPath.Trim());
        App.Settings.ExcludedPaths = ExcludedPaths.ToList();
        NewExcludedPath = "";
        Save();
    }

    [RelayCommand]
    private void RemoveExcludedPath(string path)
    {
        ExcludedPaths.Remove(path);
        App.Settings.ExcludedPaths = ExcludedPaths.ToList();
        Save();
    }

    private void Save() => App.Settings.Save();
}
