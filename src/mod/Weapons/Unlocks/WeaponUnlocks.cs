using HarmonyLib;
using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.Armory;
using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.Player;
using Il2CppSG.Claymore.SaveSystem;
using Il2CppSG.Claymore.UI;
using Il2CppSG.GlobalEvents.Variables;
using UnityEngine;

namespace UntilYouFallArchipelago;

internal sealed class WeaponUnlocks
{
    private const string RightHandDominantFlagName = "Tutorial_RightHandDominant";
    private const long PeriodicSyncIntervalMs = 1000;
    private const long DefinitionRescanIntervalMs = 3000;
    private static readonly bool[] HandSides = [true, false];
    private static WeaponUnlocks _instance = null!;
    private readonly LogChannel _log;
    private readonly LogChannel _startingWeaponsLog;
    private readonly LogChannel _handsLog;
    private readonly Action<ApNotice> _notifyTechnical;
    private readonly Func<ArchipelagoClient?> _client;
    private long _nextDefinitionScanAt;
    private readonly Dictionary<string, ArmamentID> _definitions = new(StringComparer.OrdinalIgnoreCase);

    private HashSet<long> _allowed = [WeaponCatalog.VanillaStarter(true).Id, WeaponCatalog.VanillaStarter(false).Id];
    private HashSet<long> _handUnlocks = [];
    private HashSet<long> _allowedScratch = [];
    private HashSet<long> _handUnlocksScratch = [];
    private BoolVariable? _rightHandDominantFlag;
    private IReadOnlyList<long> _startingWeapons = Array.Empty<long>();
    private int _freshProfile = -1;
    private StartingHands? _startingHands;
    private ArmamentInventory? _inventory;
    private bool _giving;
    private bool _ready;
    private bool _visualsEnabled = true;

    public bool HasApLoadout => _startingWeapons.Count > 0;

    public WeaponUnlocks(
        HarmonyLib.Harmony harmony,
        Action<string> log,
        Action<ApNotice> notifyTechnical,
        Func<ArchipelagoClient?> client
    )
    {
        _instance = this;
        _log = log.Channel("APWeapons");
        _startingWeaponsLog = log.Channel("APStartingWeapons");
        _handsLog = log.Channel("APHands");
        _notifyTechnical = notifyTechnical;
        _client = client;
        harmony.Patch(
            AccessTools.Method(typeof(ProfileUI), nameof(ProfileUI.LoadProfile)),
            prefix: new HarmonyMethod(typeof(WeaponUnlocks), nameof(BeforeProfileLoad)),
            postfix: new HarmonyMethod(typeof(WeaponUnlocks), nameof(AfterProfileLoad))
        );
        Patch(
            harmony,
            typeof(ArmamentInventory),
            nameof(ArmamentInventory.GiveArmament),
            nameof(AllowGrant),
            [typeof(ArmamentID), typeof(int)]
        );
        Patch(harmony, typeof(ArmamentInventory), nameof(ArmamentInventory.BuyArmament), nameof(AllowPurchase));
        Patch(harmony, typeof(ArmamentInventory), nameof(ArmamentInventory.CanBuy), nameof(AllowPurchase));
        Patch(harmony, typeof(ArmamentInventory), nameof(ArmamentInventory.GetLevel), nameof(ReadLevel));
        Patch(harmony, typeof(ArmamentInventory), nameof(ArmamentInventory.IsPurchased), nameof(ReadOwnership));
        Patch(harmony, typeof(ArmamentInventory), nameof(ArmamentInventory.LevelUp), nameof(AllowLevelUp));
        Patch(harmony, typeof(WeaponRackSlot), nameof(WeaponRackSlot.Bind), nameof(AllowRackBind));
        harmony.Patch(
            AccessTools.PropertyGetter(typeof(WeaponRackSlot), nameof(WeaponRackSlot.CanAfford)),
            prefix: new HarmonyMethod(typeof(WeaponUnlocks), nameof(RackCanAfford))
        );
        harmony.Patch(
            AccessTools.PropertyGetter(typeof(PurchaseInteractable), nameof(PurchaseInteractable.CanCrush)),
            prefix: new HarmonyMethod(typeof(WeaponUnlocks), nameof(CanCrush))
        );
        Patch(harmony, typeof(PlayerHand), nameof(PlayerHand.Bind), nameof(BindDefinition), [typeof(ArmamentID)]);
        Patch(harmony, typeof(PlayerHand), nameof(PlayerHand.Bind), nameof(BindInstance), [typeof(Armament)]);
        Patch(harmony, typeof(PlayerHand), nameof(PlayerHand.Rebind), nameof(BindInstance));
        Patch(harmony, typeof(PlayerHand), "DoGrabStart", nameof(AllowGrab), [typeof(HandInteractable)]);
        foreach (
            var type in new[]
            {
                typeof(HandInteractable),
                typeof(Armament),
                typeof(CrushableHeart),
                typeof(CrushInteractable),
                typeof(PurchaseInteractable),
                typeof(RewardInteractable),
                typeof(TwoHandedSecondaryGrip),
            }
        )
            Patch(
                harmony,
                type,
                nameof(HandInteractable.GrabStart),
                nameof(AllowDirectGrabStart),
                [typeof(PlayerHand)]
            );
        harmony.Patch(
            AccessTools.Method(typeof(WeaponRackSlot), nameof(WeaponRackSlot.Refresh)),
            postfix: new HarmonyMethod(typeof(WeaponUnlocks), nameof(RefreshSlot))
        );
        harmony.Patch(
            AccessTools.Method(typeof(WeaponRackSlot), nameof(WeaponRackSlot.DisplayVisuals)),
            postfix: new HarmonyMethod(typeof(WeaponUnlocks), nameof(RefreshSlot))
        );
        _ = new WeaponPurchaseUi(harmony, IsLocked, _log.Sink);
        _ = new WeaponHintRunestones(harmony, IsLocked, client, _log.Sink);
    }

    private static void Patch(
        HarmonyLib.Harmony harmony,
        Type type,
        string method,
        string prefix,
        Type[]? args = null
    ) =>
        harmony.Patch(AccessTools.Method(type, method, args), prefix: new HarmonyMethod(typeof(WeaponUnlocks), prefix));

    private static void BeforeProfileLoad(ProfileUI __instance, out bool __state)
    {
        __state = __instance._isNewGame;
        _instance._freshProfile = -1;
    }

    private static void AfterProfileLoad(ProfileUI __instance, bool __state)
    {
        if (!__state)
            return;

        _instance._freshProfile = __instance._profileIndex;
        _instance._startingWeaponsLog.Write($"New profile pending profile={__instance._profileIndex}");
    }

    public bool IsLocked(ArmamentID? id)
    {
        if (id == null)
            return false;

        var weapon = WeaponCatalog.ByGuid(id.GUID);
        return weapon != null && !_allowed.Contains(weapon.Id);
    }

    private static bool AllowGrant(ArmamentID __0) => _instance._giving || !_instance.IsLocked(__0);

    private static bool AllowPurchase(ArmamentID __0, ref bool __result)
    {
        if (!_instance.IsLocked(__0))
            return true;

        __result = false;
        return false;
    }

    private static bool ReadOwnership(ArmamentID __0, ref bool __result) => AllowPurchase(__0, ref __result);

    private static bool ReadLevel(ArmamentID __0, ref int __result)
    {
        if (!_instance.IsLocked(__0))
            return true;

        __result = -1;
        return false;
    }

    private static bool AllowLevelUp(PlayerHandState __0) => !_instance.IsLocked(__0.Armament);

    private static bool AllowRackBind(WeaponRackSlot __instance) => !_instance.IsLocked(__instance.ArmamentID);

    private static bool RackCanAfford(WeaponRackSlot __instance, ref bool __result) =>
        AllowPurchase(__instance.ArmamentID, ref __result);

    private static bool CanCrush(PurchaseInteractable __instance, ref bool __result)
    {
        if (!_instance.IsLocked(__instance.ArmamentID))
            return true;

        __result = _instance._client()?.CanHintWeapon == true;
        return false;
    }

    private static bool AllowDirectGrabStart(HandInteractable __instance, PlayerHand __0) => AllowGrab(__0, __instance);

    private static bool AllowGrab(PlayerHand __instance, HandInteractable? __0)
    {
        var hand = __instance;
        var interactable = __0;
        if (hand == null)
            return true;
        var left = _instance.HandSide(hand);
        if (!left.HasValue)
        {
            if (_instance._startingHands.HasValue && interactable != null)
                _instance._handsLog.Write(
                    $"Grab side unresolved; hand={hand.name}; kind={interactable.GetType().Name}"
                );
            return true;
        }
        if (
            !_instance._startingHands.HasValue
            || !HandUnlockRules.BlocksGrab(
                _instance._startingHands.Value,
                _instance._handUnlocks,
                left.Value,
                interactable != null
            )
        )
        {
            return true;
        }

        var sideName = left.Value ? "Left" : "Right";
        var message = $"{sideName} Hand is Locked";
        var kind = interactable == null ? "null" : interactable.GetType().Name;
        _instance._handsLog.Write($"Blocked grab; hand={sideName}; kind={kind}");
        _instance._notifyTechnical(ApNotice.Error(message));

        return false;
    }

    private static bool BindInstance(PlayerHand __instance, Armament __0)
    {
        if (!_instance.HandUnlocked(__instance))
        {
            if (__instance.BoundArmament != null)
                __instance.Unbind(true);
            return false;
        }
        if (__0 == null || !_instance.IsLocked(__0.ID))
            return true;
        if (__instance.BoundArmament == null)
        {
            var starter = _instance.Starter(__instance.State);
            if (starter != null)
                __instance.Bind(starter);
            else
                __instance.Unbind(true);
        }

        return false;
    }

    private static bool BindDefinition(PlayerHand __instance, ref ArmamentID __0)
    {
        if (!_instance.HandUnlocked(__instance))
        {
            __0 = null!;
            return true;
        }
        if (!_instance.IsLocked(__0))
            return true;

        var starter = _instance.Starter(__instance.State);
        __0 = starter!;

        return true;
    }

    private ArmamentID? Starter(PlayerHandState hand)
    {
        LoadDefinitions();
        SceneCache.Find(ref _inventory, item => item.saveManager != null);
        var left = _inventory?.leftHand.GetState().Pointer == hand.GetState().Pointer;
        if (_startingWeapons.Count == 0)
            return _definitions.GetValueOrDefault(WeaponCatalog.VanillaStarter(left).Guid);
        var dominant = RightHandDominant();
        if (!_startingHands.HasValue)
            return null;
        var starter = StartingWeaponRules.ForHand(_startingWeapons, _startingHands.Value, left, dominant);
        if (!starter.HasValue)
            return null;
        var other = left ? _inventory?.rightHand : _inventory?.leftHand;
        if (
            _startingWeapons.Count == 1
            && _startingHands == StartingHands.Both
            && other?.Armament != null
            && !IsLocked(other.Armament)
        )
            return null;

        return _definitions.GetValueOrDefault(WeaponCatalog.ById(starter.Value)!.Guid);
    }

    private BoolVariable? RightHandDominantFlag() =>
        SceneCache.Find(ref _rightHandDominantFlag, flag => flag.name == RightHandDominantFlagName);

    private bool RightHandDominant() => RightHandDominantFlag()?.Value ?? true;

    private static PlayerHandState HandOf(ArmamentInventory inventory, bool left) =>
        left ? inventory.leftHand : inventory.rightHand;

    private bool HoldsUnlockedWeapon(ArmamentInventory inventory)
    {
        foreach (var left in HandSides)
        {
            var armament = HandOf(inventory, left)?.Armament;
            if (armament != null && !IsLocked(armament))
                return true;
        }

        return false;
    }

    private bool HandUnlocked(PlayerHand hand)
    {
        if (!_startingHands.HasValue)
            return true;

        var left = HandSide(hand);
        return !left.HasValue || HandUnlockRules.IsUnlocked(_startingHands.Value, _handUnlocks, left.Value);
    }

    private bool? HandSide(PlayerHand hand)
    {
        var live = hand.inventory;
        SceneCache.Find(ref _inventory, item => item.saveManager != null);

        return HandUnlockRules.ResolveIsLeftHand(
            hand: hand.Pointer,
            state: hand.State?.GetState().Pointer ?? IntPtr.Zero,
            liveLeft: live?.leftHand?.PlayerHand?.Pointer ?? IntPtr.Zero,
            liveRight: live?.rightHand?.PlayerHand?.Pointer ?? IntPtr.Zero,
            liveLeftState: live?.leftHand?.GetState().Pointer ?? IntPtr.Zero,
            liveRightState: live?.rightHand?.GetState().Pointer ?? IntPtr.Zero,
            cachedLeft: _inventory?.leftHand?.PlayerHand?.Pointer ?? IntPtr.Zero,
            cachedRight: _inventory?.rightHand?.PlayerHand?.Pointer ?? IntPtr.Zero,
            cachedLeftState: _inventory?.leftHand?.GetState().Pointer ?? IntPtr.Zero,
            cachedRightState: _inventory?.rightHand?.GetState().Pointer ?? IntPtr.Zero
        );
    }

    private void LoadDefinitions()
    {
        var now = Environment.TickCount64;
        if (_definitions.Count >= WeaponCatalog.All.Length || now < _nextDefinitionScanAt)
            return;

        _nextDefinitionScanAt = now + DefinitionRescanIntervalMs;
        foreach (var definition in Resources.FindObjectsOfTypeAll<ArmamentID>())
            if (WeaponCatalog.ByGuid(definition.GUID) != null)
                _definitions[definition.GUID] = definition;
    }

    private static void RefreshSlot(WeaponRackSlot __instance)
    {
        if (!_instance._visualsEnabled)
            return;

        try
        {
            WeaponRackVisuals.Refresh(__instance, _instance.IsLocked(__instance.ArmamentID));
        }
        catch (Exception ex)
        {
            _instance._visualsEnabled = false;
            _instance._log.Write("Rack visuals disabled after UI error" + ex.Message);
        }
    }

    private long _nextSlotSweepAt;
    private long _nextOwnershipSyncAt;
    private bool _stateChanged;

    public void SyncState(
        IReadOnlyCollection<long> unlocks,
        IReadOnlyList<long> startingWeapons,
        StartingHands? startingHands,
        IReadOnlyCollection<long> handUnlocks
    )
    {
        if (startingWeapons.Count == 0 || !startingHands.HasValue)
            return;

        _startingWeapons = startingWeapons;
        _handUnlocksScratch.Clear();
        _handUnlocksScratch.UnionWith(handUnlocks);
        var handStateChanged = _startingHands != startingHands || !_handUnlocks.SetEquals(_handUnlocksScratch);
        _startingHands = startingHands;
        if (handStateChanged)
            (_handUnlocks, _handUnlocksScratch) = (_handUnlocksScratch, _handUnlocks);
        _allowedScratch.Clear();
        _allowedScratch.UnionWith(unlocks);
        _allowedScratch.UnionWith(startingWeapons);
        var unlocksChanged = !_allowed.SetEquals(_allowedScratch);
        if (unlocksChanged)
            (_allowed, _allowedScratch) = (_allowedScratch, _allowed);
        _stateChanged |= handStateChanged || unlocksChanged;
    }

    public void Update(LevelManager? manager)
    {
        var startingHands = _startingHands;
        if (_startingWeapons.Count == 0 || !startingHands.HasValue)
            return;
        if (manager == null || !manager.IsGameplayRoom())
        {
            if (_ready)
                _log.Write("Gameplay inventory suspended");
            _ready = false;

            return;
        }

        var changed = _stateChanged;
        _stateChanged = false;
        LoadDefinitions();
        var inventory = manager.armamentInventory;
        if (!inventory.HasProfile())
            return;

        changed |= !_ready || _inventory == null || _inventory.Pointer != inventory.Pointer;
        _ready = true;
        _inventory = inventory;
        var granted = false;
        var now = Environment.TickCount64;
        if (changed || now >= _nextOwnershipSyncAt)
        {
            _nextOwnershipSyncAt = now + PeriodicSyncIntervalMs;
            granted = GrantMissingWeapons(inventory);
        }
        if (granted)
            inventory.saveManager.SaveEconomy();
        EquipFreshProfile(manager, inventory);
        foreach (var left in HandSides)
        {
            var hand = HandOf(inventory, left);
            if (hand == null || hand.PlayerHand == null)
                continue;
            var bound = hand.PlayerHand.BoundArmament;
            if (!HandUnlockRules.IsUnlocked(startingHands.Value, _handUnlocks, left))
            {
                if (hand.Armament != null || bound != null)
                    hand.PlayerHand.Unbind(true);
                continue;
            }
            if (!IsLocked(hand.Armament) && (bound == null || !IsLocked(bound.ID)))
                continue;
            var starter = Starter(hand);
            if (starter != null)
                hand.PlayerHand.Bind(starter);
            else
                hand.PlayerHand.Unbind(true);
        }
        if (!HoldsUnlockedWeapon(inventory))
        {
            var starterLeft = HandUnlockRules.ShouldCarryStarter(
                startingHands.Value,
                left: true,
                rightDominant: RightHandDominant()
            );
            var hand = HandOf(inventory, starterLeft);
            var starter = hand == null ? null : Starter(hand);
            if (starter != null && hand?.PlayerHand != null)
                hand.PlayerHand.Bind(starter);
        }
        if (changed || granted)
        {
            inventory.RefreshAll();
            foreach (var slot in UnityEngine.Object.FindObjectsOfType<WeaponRackSlot>())
                slot.Refresh();

            _log.Write($"Ownership refreshed unlocked={_allowed.Count}/{WeaponCatalog.All.Length}");
        }
        if (now < _nextSlotSweepAt)
            return;

        _nextSlotSweepAt = now + PeriodicSyncIntervalMs;
        foreach (var slot in UnityEngine.Object.FindObjectsOfType<WeaponRackSlot>())
            RefreshSlot(slot);
    }

    private bool GrantMissingWeapons(ArmamentInventory inventory)
    {
        var granted = false;
        foreach (var weapon in WeaponCatalog.All)
        {
            if (!_definitions.TryGetValue(weapon.Guid, out var definition))
                continue;
            var owned = _allowed.Contains(weapon.Id);
            if (owned ? inventory.IsPurchased(definition) : inventory.Contains(definition))
                continue;

            _giving = true;
            try
            {
                inventory.GiveArmament(definition, owned ? 0 : -1);
            }
            finally
            {
                _giving = false;
            }
            granted = true;
            _log.Write(
                $"{(owned ? "Granted" : "Registered locked")} {weapon.Name} guid={weapon.Guid} level={inventory.GetLevel(definition)}"
            );
        }

        return granted;
    }

    private void EquipFreshProfile(LevelManager? manager, ArmamentInventory inventory)
    {
        if (
            _startingWeapons.Count != 2
            || _freshProfile != inventory.saveManager.ProfileIndex
            || !manager.InRuneforge()
        )
            return;
        var dominant = RightHandDominantFlag();
        if (dominant == null)
            return;
        foreach (var left in HandSides)
        {
            var hand = HandOf(inventory, left);
            var id = StartingWeaponRules.ForHand(_startingWeapons, _startingHands!.Value, left, dominant.Value);
            if (
                id.HasValue
                && (hand?.PlayerHand == null || !_definitions.ContainsKey(WeaponCatalog.ById(id.Value)!.Guid))
            )
                return;
        }

        foreach (var left in HandSides)
        {
            var hand = HandOf(inventory, left);
            var id = StartingWeaponRules.ForHand(_startingWeapons, _startingHands!.Value, left, dominant.Value);
            if (!id.HasValue)
                continue;
            var definition = _definitions[WeaponCatalog.ById(id.Value)!.Guid];
            if (hand.Armament?.GUID != definition.GUID)
                hand.PlayerHand.Bind(definition);

            _startingWeaponsLog.Write(
                $"Initial equip profile={_freshProfile} rightDominant={dominant.Value} left={left} weapon={WeaponCatalog.ById(id.Value)!.Name}"
            );
        }
        _freshProfile = -1;
    }
}
