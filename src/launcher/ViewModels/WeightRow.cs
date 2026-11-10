using System.ComponentModel;

namespace UntilYouFall.Launcher.ViewModels;

internal sealed class WeightRow(string name) : INotifyPropertyChanged
{
    private int _weight;
    private int _total;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? WeightChanged;

    public string Name { get; } = name;
    public string Percent => _total == 0 ? "off" : $"{100.0 * _weight / _total:0.#}%";

    public int Weight
    {
        get => _weight;
        set
        {
            var next = Math.Clamp(value, 0, 100);

            if (next == _weight)
                return;
            _weight = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Weight)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Amount)));
            WeightChanged?.Invoke();
        }
    }

    public decimal Amount
    {
        get => _weight;
        set => Weight = (int)Math.Round(value);
    }

    public int Total
    {
        set
        {
            _total = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Percent)));
        }
    }
}
