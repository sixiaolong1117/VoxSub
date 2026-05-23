using System;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace VoxSub.Services;

/// <summary>
/// XAML markup extension for localized strings.
/// Usage: Text="{services:Localization Key=SomeKey}"
/// </summary>
public class LocalizationExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public LocalizationExtension()
    {
    }

    public LocalizationExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var localization = Localization.Instance;

        // Create a binding to the Strings indexer so UI updates when language changes
        var binding = new Binding
        {
            Source = localization,
            Path = $"Strings[{Key}]",
            Mode = BindingMode.OneWay
        };

        return binding;
    }
}