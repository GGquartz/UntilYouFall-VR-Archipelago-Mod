using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using UntilYouFall.Launcher.ViewModels;

namespace UntilYouFall.Launcher.Views;

public partial class ItemPicker : UserControl
{
    public ItemPicker()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is OptionViewModel option)
                Chips.ItemsSource = option.Items;
        };
    }

    private void ShowMatches()
    {
        if (DataContext is not OptionViewModel option)
            return;
        var text = Search.Text ?? "";
        Matches.ItemsSource = option
            .Suggestions.Where(name => name.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList();
        Dropdown.IsOpen = true;
    }

    private void Search_GotFocus(object? sender, GotFocusEventArgs args) => ShowMatches();

    private void Search_PointerPressed(object? sender, PointerPressedEventArgs args) => ShowMatches();

    private void Search_TextChanged(object? sender, TextChangedEventArgs args)
    {
        if (Search.IsFocused)
            ShowMatches();
    }

    private void Matches_SelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (Matches.SelectedItem is not string name || DataContext is not OptionViewModel option)
            return;
        option.AddItem(name);
        Matches.SelectedItem = null;
        Dropdown.IsOpen = false;
        Search.Text = "";
    }

    private static SelectedItem? Chip(object? sender) => (sender as Control)?.DataContext as SelectedItem;

    private void Minus_Click(object? sender, RoutedEventArgs args)
    {
        if (Chip(sender) is { } chip)
            chip.Count--;
    }

    private void Plus_Click(object? sender, RoutedEventArgs args)
    {
        if (Chip(sender) is { } chip)
            chip.Count++;
    }

    private void Remove_Click(object? sender, RoutedEventArgs args)
    {
        if (Chip(sender) is { } chip && DataContext is OptionViewModel option)
            option.RemoveItem(chip);
    }
}
