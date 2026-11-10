using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.UI.Inspection;
using Il2CppSG.Claymore.UI.Notification;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UntilYouFallArchipelago;

internal sealed class WeaponAbilityLocks
{
    private const string LockedOverlayName = "AP Locked Ability Overlay";
    private static WeaponAbilityLocks _instance = null!;
    private static readonly Dictionary<IntPtr, (Color Name, Color Charge, Color Description, Color Panel)> LqColors =
    [];

    private static readonly Color LockedColor = ApColors.SalmonColor;
    private readonly LogChannel _abilitiesLog;
    private bool _separateItems;
    private HashSet<long> _unlocks = [];

    public WeaponAbilityLocks(HarmonyLib.Harmony harmony, Action<string> log)
    {
        _instance = this;
        _abilitiesLog = log.Channel("APAbilities");
        harmony.Patch(
            AccessTools.Method(typeof(ArmamentAbilityUser), nameof(ArmamentAbilityUser.SetAbility)),
            postfix: new HarmonyMethod(typeof(WeaponAbilityLocks), nameof(Refresh))
        );
        harmony.Patch(
            AccessTools.Method(typeof(ArmamentAbilityUser), nameof(ArmamentAbilityUser.ChargeSuper), Type.EmptyTypes),
            prefix: new HarmonyMethod(typeof(WeaponAbilityLocks), nameof(AllowCharge))
        );
        harmony.Patch(
            AccessTools.Method(
                typeof(ArmamentAbilityUser),
                nameof(ArmamentAbilityUser.ChargeSuper),
                [typeof(float), typeof(bool)]
            ),
            prefix: new HarmonyMethod(typeof(WeaponAbilityLocks), nameof(AllowCharge))
        );
        harmony.Patch(
            AccessTools.Method(typeof(ArmamentAbilityUser), "OnWeaponCharged"),
            prefix: new HarmonyMethod(typeof(WeaponAbilityLocks), nameof(AllowCharge))
        );
        harmony.Patch(
            AccessTools.Method(typeof(AbilityChargeUINotifierInstanced), "OnFullCharge"),
            prefix: new HarmonyMethod(typeof(WeaponAbilityLocks), nameof(AllowChargeNotification))
        );
        harmony.Patch(
            AccessTools.Method(typeof(InspectionUIPanel), nameof(InspectionUIPanel.Display)),
            postfix: new HarmonyMethod(typeof(WeaponAbilityLocks), nameof(UpdateInspectionPanel))
        );
        harmony.Patch(
            AccessTools.Method(typeof(InspectionUIPanelLQ), nameof(InspectionUIPanelLQ.Display)),
            postfix: new HarmonyMethod(typeof(WeaponAbilityLocks), nameof(UpdateInspectionPanelLq))
        );
    }

    public bool Locked(string? guid) =>
        WeaponAbilityRules.Locked(_separateItems, WeaponCatalog.ByGuid(guid ?? ""), _unlocks);

    public void Update(bool separateItems, IReadOnlyCollection<long> unlocks)
    {
        if (_separateItems == separateItems && _unlocks.SetEquals(unlocks))
            return;

        _separateItems = separateItems;
        _unlocks = [.. unlocks];
        _abilitiesLog.Write($"Super unlocks refreshed separate={_separateItems} count={_unlocks.Count}");

        foreach (var user in UnityEngine.Object.FindObjectsOfType<ArmamentAbilityUser>())
            Refresh(user);
        foreach (var panel in UnityEngine.Object.FindObjectsOfType<InspectionPanel>())
        {
            if (!panel.IsActive || panel.activeHand == null || panel.crushInteractableSource == null)
                continue;
            try
            {
                panel.Populate(panel.activeHand, panel.crushInteractableSource);
            }
            catch (Exception ex)
            {
                _abilitiesLog.Write("Active inspection refresh failed: " + ex.Message);
            }
        }
    }

    private static void Refresh(ArmamentAbilityUser __instance)
    {
        var user = __instance;
        if (!_instance.Locked(user.Armament?.ID?.GUID))
        {
            if (user.Ability == null && user.AbilityID != null)
                user.SetAbility(user.AbilityID);

            return;
        }

        var definition = user.AbilityID;
        if (user.Ability != null)
        {
            try
            {
                user.DestroyAbility();
            }
            catch (Exception ex)
            {
                _instance._abilitiesLog.Write($"DestroyAbility failed: {ex.Message}");
            }
        }
        user.AbilityID = definition;
        ResetCharge(user);
    }

    private static bool AllowCharge(ArmamentAbilityUser __instance)
    {
        if (!_instance.Locked(__instance.Armament?.ID?.GUID))
            return true;

        ResetCharge(__instance);
        return false;
    }

    private static bool AllowChargeNotification(AbilityChargeUINotifierInstanced __instance, int __0)
    {
        var users = __instance._currentArmamentAbilityUser;
        if (users == null || __0 < 0 || __0 >= users.Length)
            return true;
        var user = users[__0];
        if (user == null || !_instance.Locked(user.Armament?.ID?.GUID))
            return true;

        __instance.ResetMovableObject(__0);
        return false;
    }

    private static void ResetCharge(ArmamentAbilityUser user)
    {
        var armament = user.Armament;
        var guid = armament?.ID?.GUID;
        if (guid == null)
            return;

        ResetHandCharge(user.leftHandState, guid);
        ResetHandCharge(user.rightHandState, guid);
        if (user.chargeVariable != null)
            user.chargeVariable.Value = 0;
        user.isReady = false;
        if (armament != null)
            armament.AbilityState = AbilityState.Inactive;
        if (user.chargedActivationRoot != null)
            user.chargedActivationRoot.SetActive(false);
        if (armament?.VisualsReady == true && armament.Visuals != null)
            armament.Visuals.ChargedEffect(false);
    }

    private static void ResetHandCharge(Il2CppSG.Claymore.Player.PlayerHandState? hand, string guid)
    {
        var state = hand?.GetState();
        if (state?.Armament?.GUID == guid)
            state.SuperCharge = 0;
    }

    private static void UpdateInspectionPanel(InspectionUIPanel __instance, ArmamentID __0)
    {
        var panel = __instance.superPanel;
        if (panel == null)
            return;

        var locked = _instance.Locked(__0?.GUID);
        if (locked)
            panel.gameObject.SetActive(true);
        SetCanvasLockedOverlay(__instance, panel, locked);
    }

    private static void UpdateInspectionPanelLq(InspectionUIPanelLQ __instance, ArmamentID __0)
    {
        var panel = __instance.superPanel;
        if (panel == null)
            return;

        var locked = _instance.Locked(__0?.GUID);
        if (locked)
            panel.gameObject.SetActive(true);
        SetWorldLockedOverlay(__instance, panel, locked);
    }

    private static void SetCanvasLockedOverlay(InspectionUIPanel inspection, SuperDescriptionUI panel, bool locked)
    {
        var dim = locked ? 0.3f : 1f;
        var tint = new Color(dim, dim, dim, 1f);
        panel.tmpAbilityName?.canvasRenderer.SetColor(tint);
        panel.tmpSuperChargeTime?.canvasRenderer.SetColor(tint);
        panel.tmpAbilityDescription?.canvasRenderer.SetColor(tint);

        var existing = panel.transform.Find(LockedOverlayName)?.gameObject;
        if (existing == null && locked)
        {
            existing = new GameObject(LockedOverlayName) { layer = panel.gameObject.layer };
            var textRect = existing.AddComponent<RectTransform>();
            textRect.SetParent(panel.transform, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = existing.AddComponent<TextMeshProUGUI>();
            text.font = panel.tmpAbilityName?.font;
            text.text = "<b>LOCKED</b>";
            text.color = LockedColor;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = panel.tmpAbilityName?.fontSize ?? 20;
            text.fontSizeMax = (panel.tmpAbilityName?.fontSize ?? 20) * 2.5f;
            text.raycastTarget = false;
            text.maskable = false;
            text.isOverlay = true;
            var sourceMaterial = panel.tmpAbilityName?.fontSharedMaterial ?? text.font?.material;
            if (sourceMaterial != null)
            {
                var textMaterial = new Material(sourceMaterial) { name = "AP Locked Ability Text Material" };
                TmpOverlayMaterial.Apply(textMaterial);
                text.fontSharedMaterial = textMaterial;
            }
        }
        if (existing != null)
        {
            existing.transform.SetAsLastSibling();
            existing.SetActive(locked);
        }
    }

    private static void SetWorldLockedOverlay(InspectionUIPanelLQ inspection, SuperDescriptionUILQ panel, bool locked)
    {
        if (!LqColors.TryGetValue(panel.Pointer, out var colors))
        {
            var material = panel.panelRenderer?.material;
            colors = (
                panel.tmpAbilityName?.color ?? Color.white,
                panel.tmpSuperChargeTime?.color ?? Color.white,
                panel.tmpAbilityDescription?.color ?? Color.white,
                material != null && material.HasProperty("_Color") ? material.color : Color.white
            );
            LqColors[panel.Pointer] = colors;
        }

        var amount = locked ? 0.3f : 1f;
        if (panel.tmpAbilityName != null)
            panel.tmpAbilityName.color = colors.Name * amount;
        if (panel.tmpSuperChargeTime != null)
            panel.tmpSuperChargeTime.color = colors.Charge * amount;
        if (panel.tmpAbilityDescription != null)
            panel.tmpAbilityDescription.color = colors.Description * amount;
        var panelMaterial = panel.panelRenderer?.material;
        if (panelMaterial != null && panelMaterial.HasProperty("_Color"))
            panelMaterial.color = colors.Panel * amount;

        var existing = panel.transform.Find(LockedOverlayName)?.gameObject;
        if (existing == null && locked && panel.tmpAbilityName != null)
        {
            existing = UnityEngine.Object.Instantiate(panel.tmpAbilityName.gameObject, panel.transform, false);
            existing.name = LockedOverlayName;
            var text = existing.GetComponent<TextMeshPro>();
            text.text = "LOCKED";
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = panel.tmpAbilityName.fontSize * 2f;
            var rect = text.rectTransform;
            var descriptionPosition = panel.tmpAbilityDescription?.rectTransform.localPosition ?? rect.localPosition;
            rect.localPosition = Vector3.Lerp(
                panel.tmpAbilityName.rectTransform.localPosition,
                descriptionPosition,
                0.5f
            );
            rect.sizeDelta = panel.tmpAbilityName.rectTransform.sizeDelta * 2f;
            existing.transform.SetAsLastSibling();
        }
        if (existing != null)
        {
            existing.transform.SetAsLastSibling();
            var text = existing.GetComponent<TextMeshPro>();
            if (text != null)
            {
                text.sortingLayerID = panel.tmpAbilityName?.sortingLayerID ?? text.sortingLayerID;
                text.sortingOrder =
                    Math.Max(
                        panel.panelRenderer?.sortingOrder ?? 0,
                        Math.Max(
                            panel.tmpAbilityName?.sortingOrder ?? 0,
                            Math.Max(
                                panel.tmpSuperChargeTime?.sortingOrder ?? 0,
                                panel.tmpAbilityDescription?.sortingOrder ?? 0
                            )
                        )
                    ) + 1;
                IncludeInSnapshot(inspection, text);
            }

            existing.SetActive(locked);
            inspection.RefreshRenderRT();
        }
    }

    private static void IncludeInSnapshot(InspectionUIPanelLQ panel, TextMeshPro overlay)
    {
        var texts = panel._textsToSnapshot;
        if (texts == null)
            return;
        for (var i = 0; i < texts.Length; i++)
            if (texts[i]?.Pointer == overlay.Pointer)
                return;

        var expanded = new Il2CppReferenceArray<TextMeshPro>(texts.Length + 1);
        for (var i = 0; i < texts.Length; i++)
            expanded[i] = texts[i];
        expanded[texts.Length] = overlay;
        panel._textsToSnapshot = expanded;
    }
}
