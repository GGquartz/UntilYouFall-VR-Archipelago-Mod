using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.UI;
using Il2CppSG.XR;
using Il2CppTMPro;
using Il2CppValve.VR;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.UI;
using UntilYouFall.Shared;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed partial class QuestBoard : IDisposable
{
    private const string ClickClipName = "ui_CLAY_menu_click_01";
    private const string FillClipName = "ui_CLAY_menu_fill";

    private static readonly Vector3 BoardPosition = new(4.9292254f, 1.6062635f, 18.079004f);
    private const float BoardYaw = 48.207134f;
    private const float Scale = 0.0007f * 2.182873f;

    private const float HoldSeconds = 0.6f;
    private const float NavigationHoldSeconds = 0.02f;
    private const int PreviousPageLogIndex = 100;
    private const int NextPageLogIndex = 101;
    private const float PokeDepth = 90f;
    private const float MaxPointDistance = 3f;

    private const float InnerW = 1500f;
    private const float InnerH = 900f;
    private const float BoundsW = 1570f;
    private const float BoundsH = 960f;
    private const float BoundsOffsetY = 30f;
    private const float PageTop = 450f;
    private const float PanelHeightFix = 0.37f;

    private const float CardW = 700f;
    private const float CardPitch = 200f;
    private const float SectionPitch = 660f;
    private const float ColumnX = 365f;

    private sealed class Card
    {
        public RectTransform Button = null!;
        public RectTransform Fill = null!;
        public Image Face = null!;
        public TextMeshProUGUI RewardText = null!;

        public string Revealed = "";

        public float Hold;
        public bool NeedRelease;
        public bool LoggedHover;
        public bool Done;

        public Action? Refresh;
        public Action? OnActivate;
        public int Index;
        public bool IsNavigation;
        public int Section = -1;
        public float FillWidth = 110f;
        public GameObject Owner = null!;
    }

    private readonly LogChannel _log;
    private readonly ArchipelagoClient _client;
    private readonly Action<ApNotice> _notify;
    private RectTransform? _root;
    private long _retryAt;
    private long _nextRefreshAt;
    private int _builtQuestCount = -1;
    private int _builtHintCost = -1;
    private static readonly Dictionary<string, Sprite?> Icons = [];
    private readonly List<Card> _cards = [];
    private readonly List<GameObject> _pages = [];
    private int _page;
    private RectTransform? _pointer;
    private PlayerHand[] _hands = Array.Empty<PlayerHand>();
    private LevelManager? _levelManager;
    private long _nextInputRefreshAt;
    private readonly List<(Transform T, Vector3 Pos, Quaternion Rot, string Path)> _aims = [];
    private static Sprite? _ring;
    private GameObject? _board;
    private TMP_FontAsset? _font;
    private Material? _material;

    public QuestBoard(Action<string> log, ArchipelagoClient client, Action<ApNotice> notify)
    {
        _log = log.Channel("APQuestBoard");
        _client = client;
        _notify = notify;
    }

    public bool Active => _board != null;

    public void Start(LevelManager? levelManager, bool ignoreLoading = false)
    {
        Dispose();
        _levelManager = levelManager;
        if (!levelManager.InRuneforge(ignoreLoading))
        {
            _log.Write("Start rejected - not in runeforge");
            return;
        }
        var camera = SceneCache.StereoCamera();
        if (camera == null)
        {
            _log.Write("No camera");
            return;
        }
        var layer = WorldTextRules.FirstVisibleLayer(camera.cullingMask);
        var source = SceneCache.MenuFontSource();
        if (layer < 0 || source == null)
        {
            _log.Write("Layer or menu font unavailable");
            return;
        }
        _font = source.font;
        _material = new Material(source.fontSharedMaterial);
        if (_material.HasProperty("_ZTestMode"))
            _material.SetInt("_ZTestMode", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
        if (_material.HasProperty("_FaceColor"))
            _material.SetColor("_FaceColor", Color.white);

        _board = new GameObject("AP Quest Board") { layer = layer };
        var root = _board.AddComponent<RectTransform>();
        root.position = BoardPosition;
        root.rotation = Quaternion.identity;
        root.localScale = Vector3.one * Scale;
        _root = root;
        root.sizeDelta = new Vector2(InnerW, InnerH);
        var canvas = _board.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 30000;

        var background = BuildBackground(root);
        Label(
            "Title",
            root,
            new Vector2(0f, BoundsOffsetY + BoundsH / 2f + 307f),
            new Vector2(1200f, 150f),
            "QUEST BOARD",
            130,
            Color.white,
            TextAlignmentOptions.Center
        );
        Label(
            "HintCost",
            root,
            new Vector2(0f, PageTop - 816f),
            new Vector2(1200f, 130f),
            QuestChecks.HintCostText(_client.QuestHintCost)
                + (_client.QuestHintCost > 0 ? " " + RuneforgeShopRules.DefaultPricePrefix : ""),
            85,
            ApColors.QuestBoardAccentColor,
            TextAlignmentOptions.Center
        );
        string[] roman = ["I.", "II.", "III."];
        for (var section = 0; section < 3; section++)
        {
            var page = NewRect("Page" + section, root, Vector2.zero, new Vector2(InnerW, InnerH));
            _pages.Add(page.gameObject);
            Label(
                "Section" + section,
                page,
                new Vector2(0f, PageTop - 30f),
                new Vector2(240f, 70f),
                roman[section],
                64,
                Color.white,
                TextAlignmentOptions.Center
            );
            Box(
                "LineL" + section,
                page,
                new Vector2(-400f, PageTop - 30f),
                new Vector2(540f, 4f),
                ApColors.QuestBoardAccentColor
            );
            Box(
                "LineR" + section,
                page,
                new Vector2(400f, PageTop - 30f),
                new Vector2(540f, 4f),
                ApColors.QuestBoardAccentColor
            );
            var quests = _client
                .ActiveQuests.Where(quest => QuestChecks.All[quest].Tier == section + 1)
                .Take(6)
                .ToArray();

            for (var slot = 0; slot < quests.Length; slot++)
            {
                var columnX = slot % 2 == 0 ? -ColumnX : ColumnX;
                var rowY = PageTop - 90f - 100f - slot / 2 * CardPitch;
                AddCard(page, new Vector2(columnX, rowY), quests[slot]);
            }
        }
        AddArrow(root, -935f, -1);
        AddArrow(root, 935f, 1);
        RefreshInput();
        _pointer = Box("Pointer", root, Vector2.zero, new Vector2(40f, 40f), Color.white);
        _pointer.GetComponent<Image>().sprite = Ring();
        _pointer.gameObject.SetActive(false);
        _builtQuestCount = _client.ActiveQuests.Length;
        _builtHintCost = _client.QuestHintCost;
        ShowPage(0);
        SetLayerAll(_board, layer);
        FitBackground(background, BoardPosition, root.localScale.x);
        root.rotation = Quaternion.Euler(0f, BoardYaw, 0f);

        _log.Write(
            $"Board created at {BoardPosition} cards={_cards.Count} scale={Scale} background={(background != null ? "native Panel" : "none")}"
        );
    }

    private void AddCard(RectTransform root, Vector2 c, int quest)
    {
        var data = QuestChecks.All[quest];
        var gold = ApColors.GoldColor;
        var card = new Card { Index = quest, Owner = root.gameObject };
        Box("CardBack" + quest, root, c, new Vector2(CardW, 190f), ApColors.QuestBoardCardBackColor);
        var icon = Box(
            "Icon" + quest,
            root,
            c + new Vector2(-285f, 38f),
            new Vector2(110f, 110f),
            ApColors.QuestBoardIconBackColor
        );
        var sprite = IconFor(data.Icon);
        if (sprite != null)
        {
            var image = icon.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
        }
        Label(
            "Name" + quest,
            root,
            c + new Vector2(60f, 68f),
            new Vector2(540f, 40f),
            data.Name,
            32,
            Color.white,
            TextAlignmentOptions.Left
        );
        Label(
            "Desc" + quest,
            root,
            c + new Vector2(60f, 28f),
            new Vector2(540f, 44f),
            data.Description,
            19,
            ApColors.QuestBoardDescriptionColor,
            TextAlignmentOptions.Left
        );
        Box(
            "BarBack" + quest,
            root,
            c + new Vector2(10f, -10f),
            new Vector2(440f, 18f),
            ApColors.QuestBoardBarBackColor
        );
        var barFill = Fill(
            "BarFill" + quest,
            root,
            c + new Vector2(-210f, -10f),
            new Vector2(0f, 18f),
            ApColors.QuestBoardAccentColor
        );
        var progress = Label(
                "Progress" + quest,
                root,
                c + new Vector2(290f, -10f),
                new Vector2(110f, 34f),
                "",
                24,
                Color.white,
                TextAlignmentOptions.Left
            )
            .GetComponent<TextMeshProUGUI>();
        progress.enableWordWrapping = false;
        progress.enableAutoSizing = true;
        progress.fontSizeMin = 12f;
        progress.fontSizeMax = 24f;
        var border = Box(
            "BtnBorder" + quest,
            root,
            c + new Vector2(-285f, -58f),
            new Vector2(116f, 56f),
            ApColors.QuestBoardAccentColor.WithAlpha(0.7f)
        );
        card.Button = Box(
            "Btn" + quest,
            root,
            c + new Vector2(-285f, -58f),
            new Vector2(110f, 50f),
            ApColors.QuestBoardButtonColor
        );
        card.Face = card.Button.GetComponent<Image>();
        card.Fill = Fill(
            "BtnFill" + quest,
            root,
            c + new Vector2(-340f, -58f),
            new Vector2(0f, 50f),
            ApColors.QuestBoardAccentColor.WithAlpha(0.55f)
        );
        var buttonText = Label(
            "BtnText" + quest,
            root,
            c + new Vector2(-285f, -58f),
            new Vector2(110f, 50f),
            "Hint Reward",
            15,
            ApColors.QuestBoardAccentColor,
            TextAlignmentOptions.Center
        );
        var rewardsLabel = Label(
                "RewardsLabel" + quest,
                root,
                c + new Vector2(-155f, -52f),
                new Vector2(100f, 24f),
                "Rewards:",
                17,
                ApColors.QuestBoardMutedTextColor,
                TextAlignmentOptions.Left
            )
            .GetComponent<TextMeshProUGUI>();
        card.RewardText = Label(
                "RewardsText" + quest,
                root,
                c + new Vector2(112f, -52f),
                new Vector2(475f, 66f),
                "",
                17,
                ApColors.QuestBoardMutedTextColor,
                TextAlignmentOptions.Left
            )
            .GetComponent<TextMeshProUGUI>();
        var completed = Label(
            "Completed" + quest,
            root,
            c + new Vector2(60f, -52f),
            new Vector2(560f, 80f),
            "Completed",
            56,
            gold,
            TextAlignmentOptions.Center
        );

        card.Refresh = () =>
        {
            var done = _client.QuestDone(quest);
            var rows = Enumerable
                .Range(1, data.Tier)
                .Select(reward => _client.QuestRewardText(QuestChecks.LocationId(quest, reward)))
                .ToArray();
            var allShown = rows.All(row => row != null);
            var current = Math.Min(_client.QuestProgress(quest), data.Target);
            var size = barFill.sizeDelta;
            size.x = 440f * current / Math.Max(data.Target, 1);
            barFill.sizeDelta = size;
            progress.text = $"{current}/{data.Target}";
            card.RewardText.text = string.Join("\n", rows.Select(row => row ?? "???"));
            rewardsLabel.color = rows.Any(row => row != null) ? gold : ApColors.QuestBoardMutedTextColor;
            var hintable = !done && !_client.QuestHinted(quest) && !allShown;
            foreach (var part in new[] { border, card.Button, card.Fill, buttonText })
                part.gameObject.SetActive(hintable);
            foreach (var part in new[] { rewardsLabel.rectTransform, card.RewardText.rectTransform })
                part.gameObject.SetActive(!done);
            completed.gameObject.SetActive(done);
            card.Done = !hintable;
        };

        card.OnActivate = () =>
        {
            var result = _client.HintQuest(quest);
            if (result == ArchipelagoClient.QuestHintResult.NotEnoughAether)
                _notify(ApNotice.Error("Not enough Aether"));
            card.Refresh!();
        };

        card.Refresh();
        _cards.Add(card);
    }

    private Sprite? IconFor(string iconId)
    {
        if (Icons.TryGetValue(iconId, out var cached) && cached != null)
            return cached;

        try
        {
            var bytes = ReadIconBytes(iconId);
            if (bytes == null)
            {
                _log.Write($"No icon for {iconId}");
                return null;
            }
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, bytes))
            {
                _log.Write($"Icon decode failed: {iconId}");
                return null;
            }
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            Icons[iconId] = sprite;

            return sprite;
        }
        catch (Exception ex)
        {
            _log.Write($"Icon {iconId} failed: {ex.Message}");
            return null;
        }
    }

    private static byte[]? ReadIconBytes(string iconId)
    {
        if (SteamQuestIcons.IsSteamIcon(iconId))
        {
            var path = Path.Combine(
                MelonEnvironment.UserDataDirectory,
                SteamQuestIcons.FolderRelativeToUserData,
                SteamQuestIcons.FileName(iconId)
            );

            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        using var stream = typeof(QuestBoard).Assembly.GetManifestResourceStream(
            $"UntilYouFall.QuestIcons.{iconId}.jpg"
        );
        if (stream == null)
            return null;

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return buffer.ToArray();
    }

    public void BeforeFadeIn(LevelManager levelManager)
    {
        _retryAt = 0;
        Update(levelManager, true);
    }

    public void Update(LevelManager? levelManager, bool ignoreLoading = false)
    {
        _levelManager = levelManager;
        var wanted =
            _client.Connected
            && _client.ActiveQuests.Length > 0
            && levelManager.InRuneforge(ignoreLoading || _board != null);
        if (!wanted)
        {
            if (_board != null)
                Dispose();
            return;
        }
        if (
            _board != null
            && (_builtQuestCount != _client.ActiveQuests.Length || _builtHintCost != _client.QuestHintCost)
        )
            Dispose();
        if (_board == null)
        {
            if (Environment.TickCount64 < _retryAt)
                return;
            _retryAt = Environment.TickCount64 + 3000;
            try
            {
                Start(levelManager, ignoreLoading);
            }
            catch (Exception ex)
            {
                _log.Write("Build failed: " + ex);
                Dispose();
            }

            return;
        }
        UpdateBoard();
    }

    private void UpdateBoard()
    {
        if (_board == null)
            return;
        if (Environment.TickCount64 >= _nextRefreshAt)
        {
            _nextRefreshAt = Environment.TickCount64 + 500;
            foreach (var card in _cards)
                card.Refresh?.Invoke();
        }
        RefreshInput();
        UpdatePointer(_hands);
        var holding = false;
        foreach (var card in _cards)
        {
            if (card.Button == null)
            {
                Dispose();
                return;
            }
            if (card.Done || !card.Owner.activeInHierarchy)
            {
                card.Hold = 0f;
                continue;
            }
            var hovered = false;
            foreach (var hand in _hands)
            {
                if (hand == null || !hand.isActiveAndEnabled || !hand.gameObject.activeInHierarchy)
                    continue;
                if (!Hit(card.Button, hand.transform, out var how))
                    continue;
                if (!TriggerHeld(hand.transform))
                    continue;
                hovered = true;
                if (!card.LoggedHover)
                {
                    card.LoggedHover = true;
                }
            }
            if (!hovered)
            {
                card.NeedRelease = false;
                card.LoggedHover = false;
            }
            var need = card.IsNavigation ? NavigationHoldSeconds : HoldSeconds;
            card.Hold = hovered && !card.NeedRelease ? Math.Min(need, card.Hold + Time.unscaledDeltaTime) : 0f;
            if (!card.IsNavigation && card.Hold > 0f && card.Hold < need)
                holding = true;
            var size = card.Fill.sizeDelta;
            size.x = card.FillWidth * card.Hold / need;
            card.Fill.sizeDelta = size;
            card.Face.color =
                card.IsNavigation ? Color.clear
                : hovered ? ApColors.QuestBoardButtonHoverColor
                : ApColors.QuestBoardButtonColor;
            if (card.Hold < need)
                continue;
            card.Hold = 0f;
            card.NeedRelease = true;
            card.OnActivate?.Invoke();
            PlayClip(ClickClipName, false);
            _log.Write($"Card {card.Index} activated");
        }

        SetFillSound(holding);
    }

    private AudioSource? _audio;

    private void SetFillSound(bool on)
    {
        if (on)
        {
            if (_audio == null || !_audio.isPlaying)
                PlayClip(FillClipName, true);
        }
        else if (_audio != null && _audio.loop && _audio.isPlaying)
            _audio.Stop();
    }

    private void PlayClip(string name, bool loop)
    {
        try
        {
            var clip = SceneCache.Clip(name);
            if (clip == null)
            {
                _log.Write($"Clip not loaded name={name}");
                return;
            }
            if (_audio == null)
            {
                var host = new GameObject("Archipelago Quest Card Sound");
                Object.DontDestroyOnLoad(host);
                _audio = host.AddComponent<AudioSource>();
                _audio.spatialBlend = 0;
                _audio.playOnAwake = false;
            }
            if (clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();
            if (loop)
            {
                _audio.clip = clip;
                _audio.loop = true;
                _audio.Play();
            }
            else
            {
                _audio.Stop();
                _audio.loop = false;
                _audio.PlayOneShot(clip);
            }
        }
        catch (Exception ex)
        {
            _log.Write("Sound failed: " + ex.Message);
        }
    }

    private static Sprite Ring()
    {
        if (_ring != null)
            return _ring;

        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var gold = ApColors.GoldColor;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var radius = Mathf.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f)) / 32f;
            texture.SetPixel(
                x,
                y,
                radius > 1f || radius < 0.35f ? Color.clear
                    : radius > 0.78f ? gold
                    : ApColors.QuestBoardAccentColor
            );
        }
        texture.Apply();

        _ring = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _ring;
    }

    public void Dispose()
    {
        if (_audio != null)
            Object.Destroy(_audio.gameObject);
        _audio = null;
        _root = null;
        if (_board != null)
            Object.Destroy(_board);
        _board = null;
        if (_material != null)
            Object.Destroy(_material);
        _material = null;
        _pointer = null;
        _aims.Clear();
        _hands = Array.Empty<PlayerHand>();
        _nextInputRefreshAt = 0;
        _cards.Clear();
        _pages.Clear();
    }
}
