using System.Text.Json;
using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.UI;
using Il2CppSG.XR;
using Il2CppTMPro;
using Il2CppValve.VR;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed partial class QuestBoard : IDisposable
{
    private void UpdatePointer(PlayerHand[] hands)
    {
        if (_pointer == null || _root == null)
            return;

        var best = float.MaxValue;
        var found = Vector2.zero;
        foreach (var hand in hands)
        {
            if (hand == null || !hand.isActiveAndEnabled || !hand.gameObject.activeInHierarchy)
                continue;

            var (aimOrigin, aimDirection) = Aim(hand.transform);
            var origin = _root.InverseTransformPoint(aimOrigin);
            var direction = _root.InverseTransformDirection(aimDirection);
            if (Math.Abs(direction.z) < 1e-4f)
                continue;

            var distance = -origin.z / direction.z;
            if (distance <= 0.2f / _root.lossyScale.x || distance > RayLocalLimit() || distance >= best)
                continue;

            var point = origin + direction * distance;
            if (Math.Abs(point.x) > BoundsW / 2f + 300f || Math.Abs(point.y - BoundsOffsetY) > BoundsH / 2f)
                continue;

            best = distance;
            found = new Vector2(point.x, point.y);
        }
        _pointer.gameObject.SetActive(best < float.MaxValue);
        if (best < float.MaxValue)
            _pointer.anchoredPosition = found;
    }

    private bool Hit(RectTransform button, Transform hand, out string how)
    {
        how = "";
        var rect = button.rect;
        var touch = button.InverseTransformPoint(hand.position);
        if (Math.Abs(touch.z) < PokeDepth && rect.Contains(new Vector2(touch.x, touch.y)))
        {
            how = "poke";
            return true;
        }

        var (aimOrigin, aimDirection) = Aim(hand.transform);
        var origin = button.InverseTransformPoint(aimOrigin);
        var direction = button.InverseTransformDirection(aimDirection);
        if (Math.Abs(direction.z) < 1e-4f)
            return false;

        var distance = -origin.z / direction.z;
        if (distance <= 0f || distance > RayLocalLimit())
            return false;

        if (!rect.Contains(new Vector2(origin.x + direction.x * distance, origin.y + direction.y * distance)))
            return false;

        how = "point";
        return true;
    }

    private bool _triggerLogged,
        _triggerFailed;

    private bool TriggerHeld(Transform hand)
    {
        if (_triggerFailed)
            return true;

        try
        {
            var source = hand.name.Contains("Left", StringComparison.Ordinal)
                ? SteamVR_Input_Sources.LeftHand
                : SteamVR_Input_Sources.RightHand;
            var held = SteamVR_Actions.default_Click.GetState(source);
            if (held && !_triggerLogged)
            {
                _triggerLogged = true;
                _log.Write($"Trigger (default_Click) held on {hand.name}");
            }

            return held;
        }
        catch (Exception ex)
        {
            _triggerFailed = true;
            _log.Write("Trigger read failed: " + ex.Message);
            return true;
        }
    }

    private float RayLocalLimit() => MaxPointDistance / Math.Max(_root!.lossyScale.x, 1e-6f);

    private (Vector3, Vector3) Aim(Transform hand)
    {
        var left = hand.name.Contains("Left", StringComparison.Ordinal);
        foreach (var aim in _aims)
        {
            if (aim.T == null || !aim.T.gameObject.activeInHierarchy)
                continue;

            if (
                aim.Path.Contains("Left", StringComparison.Ordinal) == left
                && aim.Path.Contains("Right", StringComparison.Ordinal) != left
            )
                return (aim.T.TransformPoint(aim.Pos), aim.T.rotation * aim.Rot * Vector3.forward);
        }

        return (hand.position, hand.forward);
    }

    private void RefreshInput()
    {
        var now = Environment.TickCount64;
        if (now < _nextInputRefreshAt)
            return;

        _nextInputRefreshAt = now + 1000;
        _hands = _levelManager.ActiveHands().ToArray();
        if (_aims.Count == 0 || _aims.Any(aim => aim.T == null || !aim.T.gameObject.activeInHierarchy))
            FindAims();
    }

    private void FindAims()
    {
        var previous = _aims.ToArray();
        _aims.Clear();
        foreach (var model in Resources.FindObjectsOfTypeAll<XRControllerModel>())
        {
            if (model == null || !model.gameObject.scene.IsValid() || !model.gameObject.activeInHierarchy)
                continue;

            try
            {
                var node = model.XRTrackedNode.ToString();
                var path = node + "/" + model.name;
                for (var parent = model.transform.parent; parent != null; parent = parent.parent)
                    path = parent.name + "/" + path;
                var pointer = model.Pointer;
                var target = pointer != null ? pointer : model.transform;
                if (!previous.Any(aim => aim.T == target))
                    _log.Write(
                        $"Aim source {path} offsetPos={model.PointerOffsetPosition} offsetRot={model.PointerOffsetRotation.eulerAngles} pointer={(pointer != null ? pointer.name + " pos=" + pointer.position + " fwd=" + pointer.forward : "null")} model fwd={model.transform.forward}"
                    );
                _aims.Add(
                    pointer != null
                        ? (pointer, Vector3.zero, Quaternion.identity, path)
                        : (model.transform, model.PointerOffsetPosition, model.PointerOffsetRotation, path)
                );
            }
            catch (Exception ex)
            {
                _log.Write("Aim source read failed: " + ex.Message);
            }
        }
        if (_aims.Count == 0 && previous.Length > 0)
            _log.Write("PointerOffset aim sources lost, using PlayerHand.forward until refresh");
    }
}
