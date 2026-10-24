using System;
using SFS.UI;
using SFS.UI.ModGUI;
using UnityEngine;
using Button = SFS.UI.Button;

namespace NETools
{
    /// <summary>
    ///     Adds draggable-edge resizing to a Window/ClosableWindow.
    /// </summary>
    public static class WindowResizing
    {
        /// <summary>
        ///     Adds invisible drag handles along the edges and corners of a window so the player can
        ///     resize it, the same way DraggableWindowModule lets them move it by the title bar.
        ///     Uses the game's own input routing (SFS.UI.Button / TouchElements), not Unity's
        ///     EventSystems, so it doesn't need a Graphic/Image to be raycastable.
        /// </summary>
        /// <param name="window">The window to make resizable.</param>
        /// <param name="minSize">Smallest size the window can be dragged down to.</param>
        /// <param name="maxSize">Largest size the window can be dragged up to. Null for unbounded.</param>
        /// <param name="handleThickness">How many pixels wide the invisible edge grab strips are.</param>
        /// <param name="cornerSize">How many pixels square the invisible corner grab zones are.</param>
        public static void EnableResizing(this Window window, Vector2 minSize, Vector2? maxSize = null,
            float handleThickness = 12f, float cornerSize = 18f)
        {
            float inset = cornerSize / 2f;
            float half = handleThickness / 2f;

            // Edges: strips stretched along one side via anchors (so they track window size
            // automatically), inset at both ends by `inset` so they don't overlap the corner zones -
            // without that inset, the full-length E/W/N/S strips physically cover the corners and
            // steal every corner-drag input before it reaches the corner handles.
            CreateHandle(window, "Resize E", new Vector2(1, 0), new Vector2(1, 1),
                new Vector2(-half, inset), new Vector2(half, -inset), ResizeCursorShape.Horizontal,
                data => Resize(window, data, minSize, maxSize, horizontal: true, positiveEdge: true));
            CreateHandle(window, "Resize W", new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(-half, inset), new Vector2(half, -inset), ResizeCursorShape.Horizontal,
                data => Resize(window, data, minSize, maxSize, horizontal: true, positiveEdge: false));
            CreateHandle(window, "Resize N", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(inset, -half), new Vector2(-inset, half), ResizeCursorShape.Vertical,
                data => Resize(window, data, minSize, maxSize, horizontal: false, positiveEdge: true));
            CreateHandle(window, "Resize S", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(inset, -half), new Vector2(-inset, half), ResizeCursorShape.Vertical,
                data => Resize(window, data, minSize, maxSize, horizontal: false, positiveEdge: false));

            // Corners: small squares centered exactly on each corner point, on top of (and taking
            // input priority over) the now-inset edges.
            CreateCornerHandle(window, "Resize NE", new Vector2(1, 1), inset, minSize, maxSize, positiveX: true, positiveY: true, ResizeCursorShape.DiagonalNeSw);
            CreateCornerHandle(window, "Resize NW", new Vector2(0, 1), inset, minSize, maxSize, positiveX: false, positiveY: true, ResizeCursorShape.DiagonalNwSe);
            CreateCornerHandle(window, "Resize SE", new Vector2(1, 0), inset, minSize, maxSize, positiveX: true, positiveY: false, ResizeCursorShape.DiagonalNwSe);
            CreateCornerHandle(window, "Resize SW", new Vector2(0, 0), inset, minSize, maxSize, positiveX: false, positiveY: false, ResizeCursorShape.DiagonalNeSw);
        }

        static void CreateCornerHandle(Window window, string name, Vector2 corner, float inset,
            Vector2 minSize, Vector2? maxSize, bool positiveX, bool positiveY, ResizeCursorShape cursor)
        {
            CreateHandle(window, name, corner, corner, new Vector2(-inset, -inset), new Vector2(inset, inset), cursor, data =>
            {
                Resize(window, data, minSize, maxSize, horizontal: true, positiveEdge: positiveX);
                Resize(window, data, minSize, maxSize, horizontal: false, positiveEdge: positiveY);
            });
        }

        /// <summary>
        ///     offsetMin/offsetMax are distances from the anchor rectangle's own bottom-left/top-right
        ///     corners - unlike sizeDelta+anchoredPosition+pivot, they fully define a rect's shape
        ///     without needing to reason about pivot at all, which is what makes edges (stretched
        ///     anchors) and corners (coincident anchors) expressible with the same simple code path.
        /// </summary>
        static void CreateHandle(Window window, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, ResizeCursorShape cursor, Action<SFS.Input.OnInputStayData> onHold)
        {
            GameObject handleObject = new(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)handleObject.transform;
            rect.SetParent(window.rectTransform, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            Button button = handleObject.AddComponent<Button>();
            // Button.Start() unconditionally does `onHold += data => holdEvent.Invoke(data)` (and the
            // same for clickEvent on click). Builder.CreateButton avoids this because it instantiates
            // a prefab where the Editor already serialized these to empty UnityEvents; a bare
            // AddComponent<Button>() does not, so without this they'd stay null and Start()'s own
            // subscriber would throw the moment a handle is held.
            button.holdEvent = new HoldUnityEvent();
            button.clickEvent = new ClickUnityEvent();
            button.onHold += onHold;

            ResizeCursor.Register(rect, cursor);
            // The handle is a few pixels wide and a fast drag routinely moves the mouse off it
            // between frames, so lock the cursor to this handle's shape for the whole hold rather
            // than re-deriving it from hover each frame.
            button.onDown += _ => ResizeCursor.BeginDrag(cursor);
            button.onUp += _ => ResizeCursor.EndDrag();
        }

        /// <summary>
        ///     Resizes one axis while keeping the edge opposite the dragged one visually fixed. Reads
        ///     the window's actual RectTransform pivot rather than assuming a centered one: e.g. this
        ///     window prefab turns out to be top-pivoted on Y (confirmed by ClosableWindow's minimize,
        ///     which shrinks Size.y without ever touching Position), so a naive "shift position by half
        ///     the size delta" formula is only correct on an axis that's actually centered - applying
        ///     it to a top-pivoted Y axis double-moves the window when dragging the bottom edge.
        /// </summary>
        static void Resize(Window window, SFS.Input.OnInputStayData data, Vector2 minSize, Vector2? maxSize,
            bool horizontal, bool positiveEdge)
        {
            float rawDelta = horizontal ? data.delta.deltaPixel.x : data.delta.deltaPixel.y;
            float sign = positiveEdge ? 1f : -1f;
            float pivot = horizontal ? window.rectTransform.pivot.x : window.rectTransform.pivot.y;

            Vector2 size = window.Size;
            Vector2 position = window.Position;

            float minAxis = horizontal ? minSize.x : minSize.y;
            float maxAxis = horizontal ? (maxSize?.x ?? float.MaxValue) : (maxSize?.y ?? float.MaxValue);
            float oldAxis = horizontal ? size.x : size.y;
            float newAxis = Mathf.Clamp(oldAxis + sign * rawDelta, minAxis, maxAxis);
            float appliedDelta = newAxis - oldAxis;
            // Derived from: fixed-edge position = Position +/- pivot-weighted Size stays constant.
            // positiveEdge: shift = +pivot * appliedDelta. negativeEdge: shift = -(1-pivot) * appliedDelta.
            float positionShift = appliedDelta * (positiveEdge ? pivot : pivot - 1f);

            window.Size = horizontal ? new Vector2(newAxis, size.y) : new Vector2(size.x, newAxis);
            window.Position = horizontal
                ? new Vector2(position.x + positionShift, position.y)
                : new Vector2(position.x, position.y + positionShift);
        }
    }
}
