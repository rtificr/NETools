using System;
using SFS.UI.ModGUI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NETools;

/// <summary>Entry point for NETools' retained, nested UI tree API.</summary>
public static class Ui {
    public static UiNode Mount(Transform parent) => new(parent);
}

/// <summary>
/// A retained UI container. Each nesting call creates a real child GameObject immediately; this is
/// a layout DSL, not immediate mode. It intentionally reads like HTML/egui structure.
/// </summary>
public sealed class UiNode {
    public const int DEFAULT_ELEMENT_WIDTH = 200;
    public const int DEFAULT_ELEMENT_HEIGHT = 44;
    public Transform Transform { get; }

    internal UiNode(Transform transform) {
        Transform = transform;
    }

    public UiNode Column(Action<UiNode> children, FlexOptions options = null) =>
        Container(children, options ?? new FlexOptions { Direction = FlexDirection.Column });

    public UiNode Row(Action<UiNode> children, FlexOptions options = null) =>
        Container(children, options ?? new FlexOptions { Direction = FlexDirection.Row, Align = FlexAlign.Center });

    public UiNode Card(string title, Action<UiNode> children) {
        var box = Builder.CreateBox(Transform, 1, 1, 0, 0, .28f);
        box.gameObject.transform.FlexColumn(new FlexOptions {
            Gap = 8f, Padding = new RectOffset(10, 10, 10, 10), Align = FlexAlign.Stretch
        });
        Label(title, parent: box.gameObject.transform).Height(26);
        var card = new UiNode(box.gameObject.transform);
        children(card);
        return card;
    }

    public UiNode Accordion(string title, bool expanded, Action toggle, Action<UiNode> children) {
        var accordion = Container(_ => { }, new FlexOptions {
            Direction = FlexDirection.Column, Gap = 6f, Align = FlexAlign.Stretch
        });
        accordion.Button((expanded ? "▼ " : "▶ ") + title, toggle).Grow();
        if (!expanded) return accordion;
        accordion.Column(children, new FlexOptions {
            Direction = FlexDirection.Column, Gap = 6f, Padding = new RectOffset(12, 0, 0, 0)
        });
        return accordion;
    }

    public UiNode Tabs(string[] labels, int selected, Action<int> select) {
        return Row(row => {
            for (var i = 0; i < labels.Length; i++) {
                var index = i;
                var button = row.Button(labels[i], () => select(index));
                if (index == selected) button.Tint(new Color(1f, .85f, .3f));
                button.Grow();
            }
        }, new FlexOptions { Direction = FlexDirection.Row, Gap = 8f, Align = FlexAlign.Stretch });
    }

    public UiNode Grid(int columns, Action<UiNode> children, float cellHeight = 36f, float gap = 8f) {
        var holder = Builder.CreateContainer(Transform).gameObject;
        var grid = holder.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);
        grid.cellSize = new Vector2(1f, cellHeight);
        grid.spacing = new Vector2(gap, gap);
        grid.childAlignment = TextAnchor.UpperLeft;
        var node = new UiNode(holder.transform).Grow();
        children(node);
        return node;
    }

    public UiElement Label(string text, Transform parent = null, int height = DEFAULT_ELEMENT_HEIGHT) {
        var label = Builder.CreateLabel(parent ?? Transform, DEFAULT_ELEMENT_WIDTH, height, 0, 0, text);
        return new UiElement(label.gameObject);
    }

    public UiElement SmallLabel(string text, Transform parent = null) => Label(text, parent, DEFAULT_ELEMENT_HEIGHT * 3 / 5);

    public UiElement Button(string text, Action click) {
        var button = Builder.CreateButton(Transform, DEFAULT_ELEMENT_WIDTH, DEFAULT_ELEMENT_HEIGHT, 0, 0, click, text);
        return new UiElement(button.gameObject);
    }

    public UiElement TextInput(string value, Action<string> changed) {
        var input = Builder.CreateTextInput(Transform, DEFAULT_ELEMENT_WIDTH, DEFAULT_ELEMENT_HEIGHT, 0, 0, value,
            new UnityAction<string>(changed));
        return new UiElement(input.gameObject);
    }

    private UiNode Container(Action<UiNode> children, FlexOptions options) {
        var holder = Builder.CreateContainer(Transform).gameObject;
        if (options.Direction == FlexDirection.Row) holder.transform.FlexRow(options);
        else holder.transform.FlexColumn(options);
        holder.AddComponent<FlexFillParentWidth>();
        var node = new UiNode(holder.transform).Grow();
        children(node);
        return node;
    }

    public UiNode Grow(float amount = 1f) {
        Transform.Flex(grow: amount);
        return this;
    }

    public UiNode GrowY(float amount = 1f) {
        Transform.FlexHeight(grow: amount);
        return this;
    }

    public UiNode Width(float pixels) {
        Transform.Flex(preferredWidth: pixels);
        return this;
    }

    public UiNode MaxWidth(float pixels) {
        var limit = Transform.GetComponent<FlexMaxWidth>() ?? Transform.gameObject.AddComponent<FlexMaxWidth>();
        limit.Value = pixels;
        return this;
    }

    public UiNode Height(float pixels) {
        Transform.Flex(preferredHeight: pixels);
        return this;
    }

    public UiNode Tint(Color color) {
        var image = Transform.GetComponent<UnityEngine.UI.Image>();
        if (image != null) image.color = color;
        return this;
    }
}

/// <summary>Optional fluent sizing and appearance refinements for a retained UI element.</summary>
public sealed class UiElement {
    private readonly GameObject _gameObject;

    internal UiElement(GameObject gameObject) {
        _gameObject = gameObject;
        // Builder assigns a RectTransform size, but Unity layout groups do not infer preferred
        // dimensions from sizeDelta. Register it explicitly so a Row can measure its children.
        var rect = gameObject.GetComponent<RectTransform>();
        if (rect == null) return;
        var layout = gameObject.GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
        if (layout.preferredWidth < 0f) layout.preferredWidth = rect.sizeDelta.x;
        if (layout.preferredHeight < 0f) layout.preferredHeight = rect.sizeDelta.y;
    }

    public UiElement Grow(float amount = 1f) {
        _gameObject.transform.Flex(grow: amount);
        return this;
    }

    public UiElement GrowY(float amount = 1f) {
        _gameObject.transform.FlexHeight(grow: amount);
        return this;
    }

    public UiElement Width(float pixels) {
        _gameObject.transform.Flex(preferredWidth: pixels);
        return this;
    }

    public UiElement MaxWidth(float pixels) {
        var limit = _gameObject.GetComponent<FlexMaxWidth>() ?? _gameObject.AddComponent<FlexMaxWidth>();
        limit.Value = pixels;
        return this;
    }

    public UiElement Height(float pixels) {
        _gameObject.transform.Flex(preferredHeight: pixels);
        return this;
    }

    public UiElement Tint(Color color) {
        var image = _gameObject.GetComponent<UnityEngine.UI.Image>();
        if (image != null) image.color = color;
        return this;
    }
}