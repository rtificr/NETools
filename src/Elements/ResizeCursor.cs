using System.Collections.Generic;
using SFS.UI.ModGUI;
using UnityEngine;
using UnityEngine.UI;

namespace NETools
{
    /// <summary>
    ///     Which way a resize handle's arrow cursor should point.
    /// </summary>
    public enum ResizeCursorShape
    {
        Horizontal, // E/W edges
        Vertical, // N/S edges
        DiagonalNeSw, // NE/SW corners
        DiagonalNwSe, // NW/SE corners
        Move,
        Rotate
    }

    /// <summary>
    ///     Draws a UI resize cursor while hovering or dragging a resize handle. It is centralized
    ///     (one registry and driver), so independently-updating handles cannot fight over which
    ///     cursor is visible. The displayed size is independent from the PNG's source resolution.
    /// </summary>
    public static class ResizeCursor
    {
        static readonly Dictionary<RectTransform, ResizeCursorShape> handles = new();
        static ResizeCursorShape? activeDrag;
        static bool driverSpawned;
        static RectTransform cursorRoot;
        static RectTransform cursorVisual;
        static Image cursorImage;

        /// <summary>On-screen size of the UI cursor, independent from its PNG pixel dimensions.</summary>
        public static float DisplaySizePixels { get; set; } = 32f;

        public static void Register(RectTransform handle, ResizeCursorShape shape)
        {
            EnsureDriver();
            handles[handle] = shape;
        }

        public static void BeginDrag(ResizeCursorShape shape) => activeDrag = shape;
        public static void EndDrag() => activeDrag = null;

        static void EnsureDriver()
        {
            if (driverSpawned) return;
            driverSpawned = true;
            GameObject holder = new("ResizeCursorDriver");
            UnityEngine.Object.DontDestroyOnLoad(holder);
            holder.AddComponent<Driver>();
        }

        static void Tick()
        {
            // Prune handles destroyed since last frame (e.g. the window they belonged to closed).
            List<RectTransform> dead = null;
            foreach (KeyValuePair<RectTransform, ResizeCursorShape> entry in handles)
                if (entry.Key == null)
                    (dead ??= new List<RectTransform>()).Add(entry.Key);
            if (dead != null)
                foreach (RectTransform rect in dead)
                    handles.Remove(rect);

            if (activeDrag is { } dragging)
            {
                Apply(dragging);
                return;
            }

            Vector2 mouse = Input.mousePosition;
            foreach (KeyValuePair<RectTransform, ResizeCursorShape> entry in handles)
            {
                if (!entry.Key.gameObject.activeInHierarchy) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(entry.Key, mouse, null))
                {
                    Apply(entry.Value);
                    return;
                }
            }

            Apply(null);
        }

        static void Apply(ResizeCursorShape? shape)
        {
            if (shape == null) {
                if (cursorVisual != null) cursorVisual.gameObject.SetActive(false);
                Cursor.visible = true;
                return;
            }
            if (!EnsureVisual()) {
                Cursor.visible = true;
                return;
            }
            cursorImage.sprite = UiIcons.ResizeCursorSprite(shape.Value);
            cursorVisual.sizeDelta = Vector2.one * DisplaySizePixels;
            if (TryMousePosition(out var position)) cursorVisual.anchoredPosition = position;
            cursorVisual.SetAsLastSibling();
            cursorVisual.gameObject.SetActive(true);
            Cursor.visible = false;
        }

        static bool EnsureVisual() {
            if (cursorVisual != null && cursorRoot != null) return true;
            var holder = Builder.CreateHolder(Builder.SceneToAttach.CurrentScene, "NETools Resize Cursor");
            // ModGUI holders are ordinary transforms beneath a RectTransform canvas, so the
            // holder itself is not reliably usable for screen-space conversion.
            cursorRoot = holder.transform as RectTransform ?? holder.transform.parent as RectTransform;
            if (cursorRoot == null) {
                UnityEngine.Object.Destroy(holder);
                return false;
            }
            var visual = new GameObject("Cursor Image", typeof(RectTransform), typeof(Image));
            cursorVisual = visual.GetComponent<RectTransform>();
            cursorVisual.SetParent(cursorRoot, false);
            cursorVisual.anchorMin = cursorVisual.anchorMax = new Vector2(.5f, .5f);
            cursorVisual.pivot = new Vector2(.5f, .5f);
            cursorImage = visual.GetComponent<Image>();
            cursorImage.raycastTarget = false;
            return true;
        }

        static bool TryMousePosition(out Vector2 position) {
            var canvas = cursorRoot.GetComponentInParent<Canvas>();
            var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(cursorRoot, Input.mousePosition, eventCamera,
                out position);
        }

        class Driver : MonoBehaviour
        {
            void Update() => Tick();
            void OnDestroy() => Cursor.visible = true;
        }
    }
}
