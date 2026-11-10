namespace UnityEngine
{
    public class Transform
    {
        public Dictionary<string, Transform> Children { get; } = [];

        public Transform? Find(string path) => Children.GetValueOrDefault(path);

        public T? GetComponent<T>()
            where T : class => this as T;
    }

    public class RectTransform : Transform
    {
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector2 anchorMin,
            anchorMax,
            pivot;
        public Rect rect;
        public string name = "";
        public RectTransform? parent;
        public GameObject gameObject = new();

        public RectTransform[] GetComponentsInChildren<T>(bool includeInactive) => System.Array.Empty<RectTransform>();
    }

    public class GameObject
    {
        public bool activeSelf;
    }

    public struct Rect
    {
        public Vector2 size;
    }

    public struct Vector2(float horizontal, float vertical)
    {
        public float x = horizontal;
        public float y = vertical;
    }

    public static class Resources
    {
        public static object[] Objects = Array.Empty<object>();

        public static T[] FindObjectsOfTypeAll<T>() => Objects.OfType<T>().ToArray();
    }
}

namespace Il2CppSG.Claymore.ResourceLoading
{
    public class ResourceRequestKey
    {
        public string name = "";
    }
}

namespace Il2CppSG.Claymore.Interaction
{
    public class RewardInteractable
    {
        public ResourceLoading.ResourceRequestKey? inspectionPanelKey;
    }
}

namespace Il2CppSG.Claymore.UI.Inspection
{
    public class DescriptionInspectionPanel
    {
        private static int _nextId;
        private readonly int _id = ++_nextId;
        public UnityEngine.Transform transform = new();
        public string name = "test panel";

        public int GetInstanceID() => _id;

        public void Populate() { }
    }
}

namespace HarmonyLib
{
    public class Harmony
    {
        public readonly Dictionary<
            string,
            (HarmonyMethod? Prefix, HarmonyMethod? Postfix, HarmonyMethod? Finalizer)
        > Patches = [];

        public void PatchAll(Type type) { }

        public void Patch(
            System.Reflection.MethodBase original,
            HarmonyMethod? prefix = null,
            HarmonyMethod? postfix = null,
            HarmonyMethod? finalizer = null
        ) => Patches[original.Name] = (prefix, postfix, finalizer);
    }

    public enum MethodType
    {
        Normal,
        Getter,
        Setter,
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string name) { }

        public HarmonyPatch(Type type, string name, MethodType methodType) { }

        public HarmonyPatch(Type type, string name, params Type[] argumentTypes) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class HarmonyPriority : Attribute
    {
        public HarmonyPriority(int priority) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class HarmonyPrefix : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class HarmonyPostfix : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class HarmonyFinalizer : Attribute { }

    public static class Priority
    {
        public const int First = 800;
    }

    public class HarmonyMethod
    {
        public int priority;
        public readonly System.Reflection.MethodInfo Method;

        public HarmonyMethod(Type type, string name) =>
            Method = type.GetMethod(
                name,
                System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.Instance
            )!;
    }

    public static class AccessTools
    {
        public static System.Reflection.MethodInfo Method(Type type, string name) => type.GetMethod(name)!;
    }
}
