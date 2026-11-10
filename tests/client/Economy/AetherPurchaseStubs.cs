namespace Il2CppSG.Claymore.SaveSystem;

public class Currency
{
    public int Value;
}

public class ArmamentInventory
{
    public Currency? currency;
    public SaveManager? saveManager;
    public object? leftHand;
    public object? rightHand;
}

public class SaveManager
{
    public int ProfileIndex;
    public Action? OnSave;

    public void SetEconomyDirty() { }

    public void SaveEconomy() => OnSave?.Invoke();
}
