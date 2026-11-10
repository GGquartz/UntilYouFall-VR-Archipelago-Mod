using System.Reflection;
using HarmonyLib;
using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.UI.Inspection;
using Il2CppSG.ProtoFramework;
using Il2CppTMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed class RuneforgeShop : IDisposable
{
    private static RuneforgeShop _instance = null!;
    private readonly RuneforgeRackCloner _racks;
    private readonly LogChannel _log;
    private readonly ArchipelagoClient _client;
    private readonly Action<ApNotice> _notify;
    private readonly List<GameObject> _sections = [];
    private readonly List<ShopItem> _items = [];
    private int _builtUnlockCount;
    private long _retryAt;
    private RuneforgeShopStand? _stand;
    private bool _built;

    public RuneforgeShop(
        HarmonyLib.Harmony harmony,
        RuneforgeRackCloner racks,
        ArchipelagoClient client,
        Action<ApNotice> notify,
        Action<string> log
    )
    {
        _instance = this;
        _racks = racks;
        _client = client;
        _notify = notify;
        _log = log.Channel("APShop");
        harmony.Patch(
            AccessTools.Method(typeof(RewardInteractable), nameof(RewardInteractable.OnCrushComplete)),
            prefix: new HarmonyMethod(typeof(RuneforgeShop), nameof(BuyOrBlockCrush))
        );
        harmony.Patch(
            AccessTools.PropertyGetter(typeof(RewardInteractable), nameof(RewardInteractable.CanCrush)),
            prefix: new HarmonyMethod(typeof(RuneforgeShop), nameof(CanCrush))
        );
        harmony.Patch(
            AccessTools.Method(typeof(RewardInteractable), nameof(RewardInteractable.GrabStart)),
            prefix: new HarmonyMethod(typeof(RuneforgeShop), nameof(BlockLockedGrab)),
            postfix: new HarmonyMethod(typeof(RuneforgeShop), nameof(HintOnGrab))
        );
        harmony.Patch(
            AccessTools.Method(typeof(PlayerHand), "DoGrabStart", [typeof(HandInteractable)]),
            prefix: new HarmonyMethod(typeof(RuneforgeShop), nameof(BlockLockedHandGrab))
        );
        harmony.Patch(
            AccessTools.Method(typeof(DescriptionInspectionPanel), nameof(DescriptionInspectionPanel.Populate)),
            postfix: new HarmonyMethod(typeof(RuneforgeShop), nameof(CompactInspectionPanel))
        );
    }

    public void BeforeFadeIn(LevelManager levelManager)
    {
        _retryAt = 0;
        Update(levelManager, true);
    }

    public void Update(LevelManager? levelManager, bool ignoreLoading = false)
    {
        if (!levelManager.IsRuneforgeRoom() || !_client.Connected)
        {
            if (_built || _sections.Count > 0)
                Dispose();
            return;
        }
        var inRuneforge = levelManager.InRuneforge(ignoreLoading);
        if (!inRuneforge && !_built)
            return;

        if (_built)
        {
            if (_sections.Any(section => section == null) || _items.Any(item => !item.Alive))
            {
                _log.Write("Shop object lost");
                Dispose();
            }
            else if (_builtUnlockCount != _client.ShopUnlockCount)
            {
                _log.Write($"Unlock level changed {_builtUnlockCount}->{_client.ShopUnlockCount}");
                Dispose();
            }
            else
            {
                foreach (var item in _items.ToArray())
                {
                    if (item.Bought && Environment.TickCount64 >= item.DisposeAt)
                    {
                        item.Dispose();
                        _items.Remove(item);
                        _log.Write($"Removed bought {item.Name} priceLabel=destroyed");
                        continue;
                    }
                    item.UpdateVisual(_log.Sink, _client);
                }

                return;
            }
        }
        if (Environment.TickCount64 < _retryAt)
            return;
        _retryAt = Environment.TickCount64 + 3000;
        try
        {
            Build(levelManager!, ignoreLoading);
        }
        catch (Exception exception)
        {
            _log.Write($"Build failed: {exception}");
            Dispose();
        }
    }

    private void Build(LevelManager levelManager, bool ignoreLoading)
    {
        var source = FindNativePriceText();
        var stand = RuneforgeShopStand.Find();
        var ghostStyle = SceneCache.GhostStyle();
        var prefab = Resources.Load<GameObject>(RuneforgeShopRules.PrefabResource);
        var camera = Camera.main;
        if (source == null || stand == null || ghostStyle == null || prefab == null || camera == null)
        {
            _log.Write(
                $"Waiting nativePriceText={source != null} nativeStand={stand != null} ghostStyle={ghostStyle != null} prefab={prefab != null} camera={camera != null}"
            );
            return;
        }
        _stand = stand;
        _builtUnlockCount = _client.ShopUnlockCount;
        for (var index = 0; index < RuneforgeShopLayout.Sections.Length; index++)
        {
            var section =
                _racks.CreateSection(levelManager, ignoreLoading)
                ?? throw new InvalidOperationException("Rack mesh create failed");

            _sections.Add(section);
            var sectionPose = RuneforgeShopLayout.Sections[index];
            section.transform.SetPositionAndRotation(
                ToVector(sectionPose.Position),
                Quaternion.Euler(ToVector(sectionPose.Rotation))
            );
            section.transform.localScale = ToVector(sectionPose.Scale);
            var added = section
                .GetComponentsInChildren<Transform>(true)
                .Where(transform => transform.name == "AP Front Column")
                .ToArray();
            if (added.Length != RuneforgeShopLayout.FrontColumns.Length)
                throw new InvalidOperationException(
                    $"Expected {RuneforgeShopLayout.FrontColumns.Length} added columns: {added.Length}"
                );

            for (var column = 0; column < added.Length; column++)
            {
                added[column].SetParent(section.transform, true);
                var columnPose = RuneforgeShopLayout.FrontColumns[column];
                added[column].localPosition = ToVector(columnPose.Position);
                added[column].localRotation = Quaternion.Euler(ToVector(columnPose.Rotation));
                added[column].localScale = ToVector(columnPose.Scale);
            }
            var columns = section
                .GetComponentsInChildren<MeshFilter>(true)
                .Where(filter => filter.sharedMesh != null && filter.sharedMesh.name == "WeaponStand")
                .OrderBy(filter => section.transform.InverseTransformPoint(filter.transform.position).x)
                .ToArray();
            for (var column = 0; column < columns.Length; column++)
            {
                stand.AddGlow(columns[column].transform);
                var slot = SlotByColumn[column];
                if (_client.IsShopItemBought(ShopChecks.LocationId(index + 1, slot)))
                    continue;
                _items.Add(CreateItem(prefab, columns[column], index + 1, slot, source, stand, ghostStyle));
            }
        }

        _built = true;
        _log.Write(
            $"Build passed sections={_sections.Count} items={_items.Count} unlockLevel={_builtUnlockCount} font={source.font?.name}"
        );
    }

    private static readonly int[] SlotByColumn = [4, 6, 3, 5, 2, 1];

    private ShopItem CreateItem(
        GameObject prefab,
        MeshFilter column,
        int section,
        int slot,
        TMP_Text source,
        RuneforgeShopStand stand,
        ArmamentVisualPreview ghostStyle
    )
    {
        var top = column.transform.TransformPoint(new Vector3(0, column.sharedMesh.bounds.max.y, 0));
        var position = top + Vector3.up * RuneforgeShopRules.ShopItemHover;
        var root = Object.Instantiate(prefab, position, stand.ItemRotation(column.transform));
        root.name = $"AP Shop Item {section}-{slot}";
        ArmamentAugment? augment = null;
        GameObject? label = null;
        try
        {
            var reward =
                root.GetComponentInChildren<RewardInteractable>(true)
                ?? throw new InvalidOperationException("Reward prefab has no RewardInteractable");

            augment = Object.Instantiate(reward.Upgrade);
            augment.name = root.name + " Augment";
            augment.developerName = "ArchipelagoShopItem";
            augment.guid = $"archipelago-shop-item-{section}-{slot}";
            augment.displayName = new UserString { DefaultValue = $"Shop Item {section}-{slot}" };
            augment.description = new UserString { DefaultValue = "Archipelago shop item." };
            reward.Initialize(augment);
            ApRewardInspectionPanel.UseLargePanel(reward, _log.Sink, "APShop");
            reward.maxCrushSeconds = RuneforgeShopRules.CrushSeconds;
            var price = ShopChecks.Price(section);
            label = stand.CreateLabel(
                column.transform,
                $"AP Shop Price {section}-{slot}",
                RuneforgeShopRules.PriceText(source.text, price),
                source,
                _log.Sink
            );
            var locked = RuneforgeShopRules.IsPreviewLocked(section, _client.ShopUnlockCount);

            _log.Write(
                $"Item placed item={root.name} location={ShopChecks.LocationId(section, slot)} price={price} ghost={locked}"
            );
            return new ShopItem(root, reward, augment, label, locked ? ghostStyle : null, section, slot, locked);
        }
        catch
        {
            root.SetActive(false);
            Object.Destroy(root);
            if (augment != null)
                Object.Destroy(augment);
            if (label != null)
                Object.Destroy(label);
            throw;
        }
    }

    private static TMP_Text? FindNativePriceText() =>
        Resources
            .FindObjectsOfTypeAll<TMP_Text>()
            .FirstOrDefault(text =>
                text != null
                && text.gameObject.scene.IsValid()
                && text.gameObject.activeInHierarchy
                && text.text.StartsWith("<sprite", StringComparison.Ordinal)
                && HasAncestor(text.transform, "AetherCurrencyDisplay")
            );

    private static bool HasAncestor(Transform transform, string fragment)
    {
        for (var current = transform; current != null; current = current.parent)
            if (current.name.Contains(fragment, StringComparison.Ordinal))
                return true;

        return false;
    }

    private static Vector3 ToVector(Float3 value) => new(value.X, value.Y, value.Z);

    private ShopItem? Find(RewardInteractable? reward) =>
        reward == null ? null : _items.FirstOrDefault(item => item.Owns(reward));

    private static bool BlockLockedHandGrab(HandInteractable __0)
    {
        var item = _instance.Find(__0?.TryCast<RewardInteractable>());
        if (item == null || !item.Locked)
            return true;

        _instance._log.Write($"Blocked hand grab of locked item {item.Name}");
        return false;
    }

    private static bool BlockLockedGrab(RewardInteractable __instance)
    {
        var item = _instance.Find(__instance);
        if (item == null || !item.Locked)
            return true;

        _instance._log.Write($"Blocked grab of locked item {item.Name}");
        return false;
    }

    private static void HintOnGrab(RewardInteractable __instance)
    {
        var item = _instance.Find(__instance);
        if (item == null || item.Locked || item.Bought)
            return;

        _instance._client.HintStageReward(item.LocationId);
        _instance._log.Write(
            $"Grabbed {item.Name} location={item.LocationId} hint=requested canAfford={AetherFiller.CanAfford(ShopChecks.Price(item.Section))}"
        );
    }

    private long _affordCheckedAt = long.MinValue;
    private int _affordPrice;
    private bool _canAfford;

    private bool CanAffordCached(int price)
    {
        var now = Environment.TickCount64;
        if (price != _affordPrice || now - _affordCheckedAt >= 500)
        {
            _affordPrice = price;
            _affordCheckedAt = now;
            _canAfford = AetherFiller.CanAfford(price);
        }

        return _canAfford;
    }

    private static bool CanCrush(RewardInteractable __instance, ref bool __result)
    {
        var item = _instance.Find(__instance);
        if (item == null || !item.Locked && !item.Bought && _instance.CanAffordCached(ShopChecks.Price(item.Section)))
            return true;

        __result = false;
        return false;
    }

    private static bool BuyOrBlockCrush(RewardInteractable __instance)
    {
        var item = _instance.Find(__instance);
        if (item == null)
            return true;

        try
        {
            if (
                !item.Locked
                && !item.Bought
                && AetherFiller.TrySpend(
                    ShopChecks.Price(item.Section),
                    () => _instance._client.BuyShopItem(item.Section, item.Slot),
                    _instance._log.Sink
                )
            )
            {
                item.MarkBought();
                _instance._client.AddQuestProgress(QuestSource.ShopPurchase, 1);

                _instance._log.Write(
                    $"Bought {item.Name} location={item.LocationId} price={ShopChecks.Price(item.Section)}"
                );
                return false;
            }

            _instance._log.Write($"Crush refused on {item.Name} locked={item.Locked} bought={item.Bought}");
            if (!item.Locked && !item.Bought)
                _instance._notify(ApNotice.Error("Not enough Aether"));
        }
        catch (Exception exception)
        {
            _instance._log.Write($"Purchase failed on {item.Name}: {exception}");
            _instance._notify(ApNotice.Error("Shop purchase failed"));
        }

        __instance.OnCancelCrush();
        __instance.OnCrushAttemptFailed();
        return false;
    }

    private static void CompactInspectionPanel(DescriptionInspectionPanel __instance, CrushInteractable __1)
    {
        if (__1?.TryCast<RewardInteractable>() is not { } reward || _instance.Find(reward) == null)
            return;

        ApRewardInspectionPanel.Compact(__instance, _instance._log.Sink, "APShop");
    }

    public void Dispose()
    {
        _stand?.Dispose();
        _stand = null;
        foreach (var item in _items)
            item.Dispose();
        _items.Clear();
        foreach (var section in _sections)
            if (section != null)
                Object.Destroy(section);
        _sections.Clear();
        _built = false;
    }

    private sealed class ShopItem : IDisposable
    {
        private readonly GameObject _root;
        private readonly RewardInteractable _reward;
        private readonly ArmamentAugment _augment;
        private readonly GameObject _label;
        private readonly ArmamentVisualPreview? _ghostStyle;
        private IItemVisual? _visual;
        private long _visualRetryAt;
        private bool _descriptionApplied;

        public int Section { get; }
        public int Slot { get; }
        public bool Locked { get; }
        public bool Bought { get; private set; }
        public long DisposeAt { get; private set; }
        public long LocationId => ShopChecks.LocationId(Section, Slot);
        public string Name => _root.name;

        public ShopItem(
            GameObject root,
            RewardInteractable reward,
            ArmamentAugment augment,
            GameObject label,
            ArmamentVisualPreview? ghostStyle,
            int section,
            int slot,
            bool locked
        )
        {
            Section = section;
            Slot = slot;
            Locked = locked;
            _root = root;
            _reward = reward;
            _augment = augment;
            _label = label;
            _ghostStyle = ghostStyle;
        }

        public bool Alive => Bought || _root != null && _reward != null && _label != null;

        public bool Owns(RewardInteractable reward) =>
            _reward != null && _reward.GetInstanceID() == reward.GetInstanceID();

        public void MarkBought()
        {
            Bought = true;
            DisposeAt = Environment.TickCount64 + 150;
        }

        private void ApplyScoutedDescription(ArchipelagoClient client, Action<string> log)
        {
            if (_descriptionApplied || Locked || !client.TryGetStageRewardInfo(LocationId, out var info))
                return;
            _descriptionApplied = true;
            _augment.displayName = new UserString
            {
                DefaultValue =
                    $"<color=#{StageRewardInspection.Color(info)}>{StageRewardInspection.Title(info)}</color>",
            };
            _augment.description = new UserString { DefaultValue = StageRewardInspection.Description(info) };

            log.Write("APShop", $"Described {Name} location={LocationId} item='{info.ItemName}' flags={info.Flags}");
        }

        public void UpdateVisual(Action<string> log, ArchipelagoClient client)
        {
            if (Bought)
                return;
            ApplyScoutedDescription(client, log);
            if (
                !RuneforgeShopRules.ShouldAttachVisual(true, _visual != null, Environment.TickCount64 >= _visualRetryAt)
            )
                return;

            var info = client.TryGetStageRewardInfo(LocationId, out var scouted) ? scouted : null;
            _visual = ItemVisuals.TryAttach(_reward, info, log);
            if (_visual == null)
                _visualRetryAt = Environment.TickCount64 + RuneforgeShopRules.VisualRetryDelayMs;
            else if (_ghostStyle != null)
            {
                _visual.UseUnownedStyle(_ghostStyle);
                log.Write("APShop", $"Ghost applied item={_root.name} shader={_ghostStyle.unownedShader.name}");
            }
        }

        public void Dispose()
        {
            _visual?.Dispose();
            if (_label != null)
                Object.Destroy(_label);
            if (_root != null)
                Object.Destroy(_root);
            if (_augment != null)
                Object.Destroy(_augment);
        }
    }
}
