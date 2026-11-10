using HarmonyLib;
using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.Armaments.Traits;
using Il2CppSG.Claymore.Combat.CritPoints;
using Il2CppSG.Claymore.Entities;
using Il2CppSG.Claymore.Interaction.Rewards;
using Il2CppSG.Claymore.Movement.Dash;
using Il2CppSG.Claymore.SaveSystem;
using Il2CppSG.Claymore.UI.Inspection;
using Il2CppSG.GlobalEvents.Variables;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UntilYouFallArchipelago;

internal sealed partial class PlayerBonuses
{
    [HarmonyPatch(typeof(InspectionUIPanel), nameof(InspectionUIPanel.Populate))]
    [HarmonyPostfix]
    private static void OnInspectionPopulate(InspectionUIPanel __instance) => _instance.UpdateInspection(__instance);

    [HarmonyPatch(typeof(InspectionUIPanel), nameof(InspectionUIPanel.Display))]
    [HarmonyPostfix]
    private static void OnInspectionDisplay(InspectionUIPanel __instance, ArmamentID __0, Armament __1, StatSet __2)
    {
        _instance.UpdateSuperDetail(__instance, __1);
        _instance.UpdateDamageDetail(__instance, __0, __1, __2);
        _instance.UpdateStageProgress(__instance, __0);
    }

    public static Func<long, string?>? StageProgress { get; set; }
    public static Func<ArmamentID?, bool>? WeaponLocked { get; set; }

    private void UpdateStageProgress(InspectionUIPanel panel, ArmamentID id)
    {
        var name = panel.tmpNameText;
        if (name == null)
            return;

        var weapon = WeaponCatalog.ByGuid(id?.GUID ?? "");
        var progress =
            weapon == null ? null
            : WeaponLocked?.Invoke(id) == true ? "Crush to hint next weapon"
            : StageProgress?.Invoke(weapon.Id);
        var previous = name.text.IndexOf($"\n<size=60%><color=#{ApColors.Gold}>", StringComparison.Ordinal);
        var title = previous < 0 ? name.text : name.text[..previous];
        var text = progress == null ? title : $"{title}\n<size=60%>{ApColors.Rich(ApColors.Gold, progress)}</size>";
        if (name.text != text)
        {
            name.richText = true;
            name.text = text;
        }
        if (progress != null && _loggedDiagnostics.Add($"stages-cleared:{weapon!.Id}:{progress}"))
            _stageProgressLog.Write($"Weapon detail weapon={weapon.Name} {progress}");
    }

    private void UpdateDamageDetail(InspectionUIPanel panel, ArmamentID id, Armament armament, StatSet stats)
    {
        var weapon = WeaponCatalog.ByGuid(id?.GUID ?? "");
        if (weapon == null || weapon.NoDamageItems || stats == null)
            return;
        foreach (var kind in new[] { WeaponDamageKind.Health, WeaponDamageKind.Guard })
        {
            var count = _counts.GetValueOrDefault(WeaponDamageBonusRules.ItemId(weapon.Id, kind));
            if (count == 0)
                continue;
            var guard = kind == WeaponDamageKind.Guard;
            var view = guard ? panel.staminaDamageText : panel.healthDamageText;
            if (view?.tmpText == null)
                continue;

            var native = guard ? stats.StaminaDamage : stats.HealthDamage;
            var sharedDamage = view.tmpText.text.Contains('*');
            view.tmpText.richText = true;
            view.tmpText.text = WeaponDamageBonusRules.Display(native, count, sharedDamage);
            var live = armament == null ? "preview" : DamageValue(armament, kind).ToString();
            if (_loggedDiagnostics.Add($"damage-ui:{weapon.Id}:{guard}:{native}:{count}:{live}"))
                _damageLog.Write(
                    $"Inspection weapon={weapon.Name} guard={guard} native={native} bonus={count} text={view.tmpText.text} live={live}"
                );
        }
    }

    private void UpdateSuperDetail(InspectionUIPanel panel, Armament armament)
    {
        if (_counts.GetValueOrDefault(ApItems.EnhanceSuperBonusId) == 0 || armament == null)
            return;
        try
        {
            var ability = armament.Ability;
            var displayed =
                ability?.AbilityLevel > 0 ? armament.ID?.AbilityTree?.GetAbility(ability.AbilityLevel) : null;
            if (displayed != null)
                panel.superPanel?.SetAbility(displayed, false);
        }
        catch (Exception ex)
        {
            _log.Write("Super detail diagnostic failed: " + ex.Message);
        }
    }

    private void UpdateInspection(InspectionUIPanel panel)
    {
        var layout = InspectionLayoutFor(panel);
        if (layout == null || layout.NativeRows.Count == 0)
            return;

        SetTitle(layout.BonusPanel, "WEAPON BONUSES");
        var entries = PlayerBonusRules
            .ItemIds.Select(id => (Id: id, Label: PlayerBonusRules.Label(id, _counts.GetValueOrDefault(id))))
            .Where(entry => entry.Label != null)
            .ToArray();
        var activeNativeRows = layout.NativeRows.Where(row => row.gameObject.activeSelf).ToArray();
        var nativeCount = activeNativeRows.Length;
        EnsurePlayerRows(layout, entries.Length);
        var extraHeight = entries.Length == 0 ? 0f : HeaderHeight + entries.Length * RowStep;
        layout.StatsFrame.sizeDelta = layout.StatsFrameSize + Vector2.up * extraHeight;
        layout.BonusPanel.sizeDelta = layout.BonusPanelSize + Vector2.up * extraHeight;
        layout.ListRoot.sizeDelta = layout.ListRootSize + Vector2.up * extraHeight;
        var headerY =
            activeNativeRows.Length == 0
                ? layout.ListRootPosition.y
                : PlayerBonusRules.PlayerHeaderPosition(
                    layout.ListRootPosition.y,
                    activeNativeRows[^1].anchoredPosition.y,
                    activeNativeRows[^1].rect.height
                );
        var rowHeight =
            activeNativeRows.Length == 0 ? layout.NativeRows[0].rect.height : activeNativeRows[^1].rect.height;
        var firstPlayerRowY = headerY - layout.ListRootPosition.y - HeaderHeight - (RowStep - rowHeight);
        if (layout.PlayerHeader != null)
        {
            layout.PlayerHeader.gameObject.SetActive(entries.Length > 0);
            layout.PlayerHeader.anchoredPosition = new Vector2(layout.HeaderPosition.x, headerY);
        }
        for (var index = 0; index < layout.PlayerRows.Count; index++)
        {
            var row = layout.PlayerRows[index];
            var active = index < entries.Length;
            row.gameObject.SetActive(active);
            if (!active)
                continue;

            row.anchoredPosition = new Vector2(layout.RowPosition.x, firstPlayerRowY - index * RowStep);
            SetRow(row, entries[index].Id, entries[index].Label!, panel.displaySettings);
        }
    }

    private InspectionLayout? InspectionLayoutFor(InspectionUIPanel panel)
    {
        if (_inspectionLayouts.TryGetValue(panel.Pointer, out var existing))
            return existing;
        var bonusPanel = FindChild(panel.transform, "BonusPanel")?.GetComponent<RectTransform>();
        var statsFrame = bonusPanel?.parent?.GetComponent<RectTransform>();
        var listRoot = bonusPanel == null ? null : FindChild(bonusPanel, "ListRoot")?.GetComponent<RectTransform>();
        var header = bonusPanel == null ? null : FindChild(bonusPanel, "HeaderPanel")?.GetComponent<RectTransform>();
        if (bonusPanel == null || statsFrame == null || listRoot == null || header == null)
        {
            _log.Write(
                $"UGUI inspection layout missing bonus={bonusPanel != null} stats={statsFrame != null} list={listRoot != null} header={header != null}"
            );
            return null;
        }
        var nativeRows = new List<RectTransform>();
        FindRows(listRoot, nativeRows);
        if (nativeRows.Count == 0)
        {
            _log.Write("UGUI inspection BonusPanel has no native rows");
            return null;
        }
        nativeRows.Sort((left, right) => right.anchoredPosition.y.CompareTo(left.anchoredPosition.y));
        var layout = new InspectionLayout(statsFrame, bonusPanel, listRoot, header, nativeRows);
        _inspectionLayouts[panel.Pointer] = layout;

        return layout;
    }

    private void EnsurePlayerRows(InspectionLayout layout, int count)
    {
        if (layout.PlayerHeader == null)
        {
            layout.PlayerHeader = UnityEngine
                .Object.Instantiate(layout.NativeHeader.gameObject, layout.BonusPanel)
                .GetComponent<RectTransform>();
            layout.PlayerHeader.name = "AP Player Bonus Header";
            SetTitle(layout.PlayerHeader, "PLAYER BONUSES");
        }
        while (layout.PlayerRows.Count < count)
        {
            var row = UnityEngine
                .Object.Instantiate(layout.NativeRows[0].gameObject, layout.ListRoot)
                .GetComponent<RectTransform>();
            row.name = $"AP Player Bonus Row {layout.PlayerRows.Count + 1}";
            layout.PlayerRows.Add(row);
        }
    }

    private void SetRow(RectTransform row, long itemId, string label, VariableDisplaySettings? displaySettings)
    {
        var data = FindChild(row, "DataPanel");
        var text = data == null ? null : FindChild(data, "Text")?.GetComponent<TextMeshProUGUI>();
        var icon = data == null ? null : FindChild(data, "Icon")?.GetComponent<Image>();
        if (text != null)
            text.text = label;
        if (icon != null)
        {
            var sprite = SpriteFor(itemId, displaySettings);
            if (sprite != null)
                icon.sprite = sprite;
            else if (_loggedDiagnostics.Add($"missing-icon:{itemId}"))
                _log.Write($"Inspection icon missing item={itemId} preserved cloned icon");
        }
        var diff = FindChild(row, "DiffLayout");
        if (diff != null)
            diff.gameObject.SetActive(false);
    }

    private Sprite? SpriteFor(long id, VariableDisplaySettings? displaySettings)
    {
        if (id == ApItems.MaximumHealthBonusId)
        {
            var maximumHealthReward = SceneCache.Reward(MaximumHealthRewardName);
            var statTrait = maximumHealthReward?.Trait?.TryCast<StatModArmamentTrait>();
            var statSprite =
                statTrait == null || statTrait.StatMods.Length != 1
                    ? null
                    : displaySettings?.GetDisplaySettings(statTrait.StatMods[0].Variable)?.Icon;
            if (statSprite != null)
                return statSprite;
            var rewardSprite = maximumHealthReward?.IconSprite;
            if (rewardSprite != null)
                return rewardSprite;
        }
        var name = id switch
        {
            ApItems.MaximumHealthBonusId => "StatIcon_MaxHealth",
            ApItems.DashChargesBonusId => "HandUI_Icon_DashCharges",
            ApItems.DashImpactBonusId => "HandUI_Icon_DashDamage",
            ApItems.ComboLimitBonusId => "HandUI_Icon_ComboLimit",
            ApItems.CriticalWindowBonusId => "HandUI_Icon_ComboLimit",
            ApItems.DashRechargeBonusId => "HandUI_Icon_DashCharges",
            ApItems.EnhanceSuperBonusId => "HandUI_Icon_Super",
            _ => "",
        };

        return SceneCache.NamedSprite(name);
    }

    private static void SetTitle(Transform panel, string title)
    {
        var text = FindChild(panel, "DamageTitle")?.GetComponent<TextMeshProUGUI>();
        if (text != null)
            text.text = title;
    }

    private static Transform? FindChild(Transform root, string name)
    {
        if (root.name == name)
            return root;
        for (var index = 0; index < root.childCount; index++)
        {
            var found = FindChild(root.GetChild(index), name);
            if (found != null)
                return found;
        }
        return null;
    }

    private static void FindRows(Transform root, List<RectTransform> rows)
    {
        if (PlayerBonusRules.IsNativeBonusRow(root.name))
        {
            var row = root.GetComponent<RectTransform>();
            if (row != null)
                rows.Add(row);
        }
        for (var index = 0; index < root.childCount; index++)
            FindRows(root.GetChild(index), rows);
    }

    private sealed class InspectionLayout
    {
        public readonly RectTransform StatsFrame;
        public readonly RectTransform BonusPanel;
        public readonly RectTransform ListRoot;
        public readonly RectTransform NativeHeader;
        public readonly List<RectTransform> NativeRows;
        public readonly Vector2 StatsFrameSize;
        public readonly Vector2 BonusPanelSize;
        public readonly Vector2 ListRootSize;
        public readonly Vector2 ListRootPosition;
        public readonly Vector2 HeaderPosition;
        public readonly Vector2 RowPosition;
        public RectTransform? PlayerHeader;
        public readonly List<RectTransform> PlayerRows = [];

        public InspectionLayout(
            RectTransform statsFrame,
            RectTransform bonusPanel,
            RectTransform listRoot,
            RectTransform nativeHeader,
            List<RectTransform> nativeRows
        )
        {
            StatsFrame = statsFrame;
            BonusPanel = bonusPanel;
            ListRoot = listRoot;
            NativeHeader = nativeHeader;
            NativeRows = nativeRows;
            StatsFrameSize = statsFrame.sizeDelta;
            BonusPanelSize = bonusPanel.sizeDelta;
            ListRootSize = listRoot.sizeDelta;
            ListRootPosition = listRoot.anchoredPosition;
            HeaderPosition = nativeHeader.anchoredPosition;
            RowPosition = nativeRows[0].anchoredPosition;
        }
    }
}
