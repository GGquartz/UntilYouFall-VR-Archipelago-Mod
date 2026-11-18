namespace Il2CppTMPro
{
    public class TextMeshProUGUI
    {
        public string text = "";
        public UnityEngine.GameObject gameObject = new();
        public bool enableAutoSizing;
        public float fontSize = 110;
        public float fontSizeMax = 72;
    }
}

namespace Il2CppSG.Claymore.UI
{
    public class ProfileUI
    {
        public Il2CppTMPro.TextMeshProUGUI? _roomNameTextMeshPro;
        public Il2CppTMPro.TextMeshProUGUI _newGameTextMeshPro = new() { text = "New Game" };
        public PressAndHoldButton playButton = new();
        public bool _isNewGame;
        public bool isActiveAndEnabled = true;
        public int _profileIndex;
        public CurrencyVariable? _currencyVariable;
        public ProfileSaveManager? _saveManager;

        public void OnEnable() { }

        public void AttemptLoad() { }

        public void ShowNewGameMode(bool isNewGame) { }

        public void LoadProfile() { }

        public void LoadData() { }
    }

    public class PressAndHoldButton
    {
        public bool IsInteractable = true;
        public int AbortCount;

        public void OnPointerDown() { }

        public void AbortFill() => AbortCount++;
    }

    public class CurrencyVariable
    {
        public int Value;
    }

    public class ProfileSaveManager
    {
        public int EconomySaveCount;

        public void SaveEconomy() => EconomySaveCount++;
    }
}
