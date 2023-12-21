using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Linq;

public class ReorderListDemo : EditorWindow {
    [SerializeField] VisualTreeAsset visualTree;
    static ReorderListDemo window;

    [MenuItem("Tools/ReorderListDemo")]
    public static void ShowWindow() {
        window = GetWindow<ReorderListDemo>();
        window.titleContent = new GUIContent("Reorder List Demo");
        window.minSize = new Vector2(200, 350);
    }

    public void CreateGUI() {
        var root = visualTree.CloneTree();
        var manipulator = new ReorderManipulator();
        root.Q(USSClassNames.root).AddManipulator(manipulator);
        manipulator.OnIndexChange += (before, after) => {
            Debug.Log($"Item {before} changed to {after}.");
        };
        rootVisualElement.Add(root);
    }

    // Enum for classnames
    static class USSClassNames {
        const string delim = "__";
        public const string root = "reorder-test";
        public const string overlay     = root + delim + "overlay";
        public const string itemWrapper = root + delim + "wrapper";
        public const string item        = root + delim + "item";
    }
    class ReorderManipulator : PointerManipulator {
        public ReorderManipulator() {
            activators.Add(new ManipulatorActivationFilter() {button = MouseButton.LeftMouse});
        }
        VisualElement overlay;
        VisualElement wrapper;
        public int pointerId;
        protected override void RegisterCallbacksOnTarget() {
            overlay = target.Q(USSClassNames.overlay);
            wrapper = target.Q(USSClassNames.itemWrapper);
            target.RegisterCallback<PointerDownEvent>(OnStartDrag);
        }
        protected override void UnregisterCallbacksFromTarget() {
            OnRelease();
            target.UnregisterCallback<PointerDownEvent>(OnStartDrag);
        }
        public Vector3 cursorStartPos;
        public Vector3 cursorPos;
        public Vector3 startPos;
        Vector3 delta {
            get => cursorPos - cursorStartPos;
        }
        VisualElement currentItem;
        float maxHeight = float.PositiveInfinity;
        int indexBefore = -1;
        int indexAfter = -1;
        void OnStartDrag(PointerDownEvent ev) {
            pointerId = ev.pointerId;
            currentItem = ((VisualElement)ev.target).Closest(name: USSClassNames.item);
            PointerCaptureHelper.CapturePointer(target, pointerId);
            cursorStartPos = ev.position;
            indexBefore = wrapper.IndexOf(currentItem);
            target.RegisterCallback<PointerMoveEvent>(OnMove);
            target.RegisterCallback<PointerUpEvent>(OnRelease);
            maxHeight = target.layout.height - currentItem.layout.height;
            FillOverlay();
        }
        void OnMove(PointerMoveEvent ev) {
            cursorPos = ev.position;
            overlay.transform.position =
                Mathf.Clamp(startPos.y + delta.y, 0, maxHeight) * Vector3.up;
            FindClosestItem();
        }
        void FindClosestItem() {
            var overlayRect = overlay.worldBound;
            VisualElement selected = null;
            int index = 0;
            for (int i = 0; i < wrapper.childCount; i++) {
                var item = wrapper[i];
                if (item.worldBound.Overlaps(overlayRect)) {
                    if (selected == null) {
                        index = i;
                        selected = item;
                    } else if (IsElemNearer(overlayRect, selected, item)) {
                        index = i;
                        selected = item;
                        break;
                    }
                }
            }
            if (selected != null && selected != currentItem) {
                wrapper.Insert(index, currentItem);
                indexAfter = index;
            }
        }
        void FillOverlay() {
            // make overlay the same position and same large as selected item
            var rect = currentItem.layout;
            startPos = new Vector3(rect.x, rect.y, 0);
            overlay.transform.position = startPos;
            overlay.style.height = rect.height;
            // move children to overlay
            overlay.AddElements(currentItem.Children());
            // keep blanked target height
            currentItem.style.height = rect.height;
            overlay.style.display = DisplayStyle.Flex;
        }
        public delegate void IndexChangeCallback(int before, int after);
        public event IndexChangeCallback OnIndexChange;
        void OnRelease(PointerUpEvent ev = null) {
            PointerCaptureHelper.ReleasePointer(target, pointerId);
            target.UnregisterCallback<PointerMoveEvent>(OnMove);
            target.UnregisterCallback<PointerUpEvent>(OnRelease);
            RestoreItem();
            if (indexAfter < 0 || indexAfter == indexBefore) return;
            OnIndexChange.Invoke(indexBefore, indexAfter);
        }
        void RestoreItem() {
            if (currentItem != null) {
                currentItem.style.height = Length.Auto();
                currentItem.AddElements(overlay.Children());
            }
            currentItem = null;

            overlay.transform.position = Vector3.zero;
            overlay.style.display = DisplayStyle.None;
        }
        static bool IsElemNearer(Rect rect, VisualElement elemA, VisualElement elemB) {
            var O = rect.center;
            var A = elemA.worldBound.center;
            var B = elemB.worldBound.center;
            var OA = (A - O).magnitude;
            var OB = (B - O).magnitude;
            return OB < OA;
        }
    }
}

static class ElementExtensions {
    /// <summary>
    /// Extension of <see cref="member">VisualElement.Add</see>, accepting child list (IEnumerable)
    /// </summary>
    public static void AddElements(this VisualElement parent, IEnumerable<VisualElement> children) {
        var childrenCopied = children.ToArray();
        foreach (var el in childrenCopied) {
            parent.Add(el);
        }
    }
    /// <summary>
    /// Extension of <see cref="member">VisualElement.ClassListContains</see>, accepting a bunch of classNames,
    /// returns true if contains every class on the list
    /// </summary>
    public static bool HasClass(this VisualElement elem, params string[] classNames) {
        foreach (var className in classNames) {
            if (elem.ClassListContains(className))
                return true;
        }
        return false;
    }
    /// <summary>
    /// Begins with the current element, travels up the DOM tree until it finds a match for the supplied selector.
    /// A method borrowed from jQuery / Javascript DOM.
    /// </summary>
    public static T Closest<T>(this VisualElement elem, string name = null, params string[] classes)
    where T : VisualElement {
        var current = elem;
        do {
            if (
                (string.IsNullOrEmpty(name) || name == current.name) &&
                (classes.Length == 0 || current.HasClass(classes)) &&
                current is T
            ) return current as T;
            current = current.parent;
        } while(current != null);
        return null;
    }
    public static VisualElement Closest(this VisualElement elem, string name = null, params string[] classes) {
        return elem.Closest<VisualElement>(name, classes);
    }

}
