using System.ComponentModel;

namespace UntilYouFall.Launcher.ViewModels;

internal sealed class SelectedItem(string name, int max, bool counted, string tag = "") : INotifyPropertyChanged
{
    private int _count = 1;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; } = name;
    public int Max { get; } = max;
    public bool Counted { get; } = counted;
    public string Tag { get; } = tag;
    public bool HasTag => Tag.Length > 0;
    public string Label => Counted ? $"{Name} ×{Count}" : Name;

    public int Count
    {
        get => _count;
        set
        {
            value = Math.Clamp(value, 1, Max);

            if (value == _count)
                return;
            _count = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
        }
    }
}
