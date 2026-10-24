using UnityEngine;
using UnityEngine.UI;

namespace NETools;

/// <summary>CSS-inspired, declarative layout options for ModGUI containers.</summary>
public enum FlexDirection { Row, Column }
public enum FlexJustify { Start, Center, End, SpaceBetween }
public enum FlexAlign { Start, Center, End, Stretch }

public sealed class FlexOptions {
    public FlexDirection Direction = FlexDirection.Column;
    public FlexJustify Justify = FlexJustify.Start;
    public FlexAlign Align = FlexAlign.Stretch;
    public float Gap = 8f;
    public RectOffset Padding = new(0, 0, 0, 0);
}

/// <summary>
/// HTML-like layout helpers. Use <c>parent.FlexRow()</c>/<c>FlexColumn()</c>, then call
/// <c>child.Flex(grow: 1)</c> instead of assigning widths to every child.
/// </summary>
public static class FlexLayout {
    public static Transform FlexRow(this Transform parent, FlexOptions options = null) =>
        Configure(parent, options ?? new FlexOptions { Direction = FlexDirection.Row });

    public static Transform FlexColumn(this Transform parent, FlexOptions options = null) =>
        Configure(parent, options ?? new FlexOptions { Direction = FlexDirection.Column });

    public static T Flex<T>(this T item, float grow = 0f, float preferredWidth = -1f,
        float preferredHeight = -1f) where T : Component {
        var layout = item.GetComponent<LayoutElement>() ?? item.gameObject.AddComponent<LayoutElement>();
        layout.flexibleWidth = grow;
        if (preferredWidth >= 0f) layout.preferredWidth = preferredWidth;
        if (preferredHeight >= 0f) layout.preferredHeight = preferredHeight;
        return item;
    }

    /// <summary>Explicit vertical growth for a child inside a column.</summary>
    public static T FlexHeight<T>(this T item, float grow = 1f) where T : Component {
        var layout = item.GetComponent<LayoutElement>() ?? item.gameObject.AddComponent<LayoutElement>();
        layout.flexibleHeight = grow;
        return item;
    }

    private static Transform Configure(Transform parent, FlexOptions options) {
        var horizontal = parent.GetComponent<HorizontalLayoutGroup>();
        var vertical = parent.GetComponent<VerticalLayoutGroup>();
        var group = options.Direction == FlexDirection.Row
            ? (HorizontalOrVerticalLayoutGroup)(horizontal ?? parent.gameObject.AddComponent<HorizontalLayoutGroup>())
            : vertical ?? parent.gameObject.AddComponent<VerticalLayoutGroup>();
        if (horizontal != null && horizontal != group) horizontal.enabled = false;
        if (vertical != null && vertical != group) vertical.enabled = false;
        group.enabled = true;
        group.spacing = options.Gap;
        group.padding = options.Padding;
        group.childAlignment = Map(options.Justify, options.Align);
        // The main axis follows each child's preferred size/flexible setting. Only the cross axis
        // is stretched. Forcing expansion on both axes made short button rows inherit their
        // container's initial 1px height before the parent layout had measured them.
        if (options.Direction == FlexDirection.Row) {
            group.childControlWidth = true;
            group.childControlHeight = options.Align == FlexAlign.Stretch;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
        } else {
            group.childControlWidth = options.Align == FlexAlign.Stretch;
            group.childControlHeight = true;
            group.childForceExpandWidth = options.Align == FlexAlign.Stretch;
            group.childForceExpandHeight = false;
        }
        return parent;
    }

    private static TextAnchor Map(FlexJustify justify, FlexAlign align) {
        var horizontal = align == FlexAlign.Start ? 0 : align == FlexAlign.Center ? 1 : 2;
        var vertical = justify == FlexJustify.Start ? 2 : justify == FlexJustify.Center ? 1 : 0;
        return (TextAnchor)(vertical * 3 + horizontal);
    }
}
