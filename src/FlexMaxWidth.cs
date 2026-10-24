using UnityEngine;
using UnityEngine.UI;

namespace NETools;

/// <summary>Retained maximum-width constraint for Unity layout groups.</summary>
internal sealed class FlexMaxWidth : MonoBehaviour {
    public float Value;
    private RectTransform rect = null!;
    private LayoutElement layout = null!;

    private void Awake() {
        rect = (RectTransform)transform;
        layout = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
    }

    private void LateUpdate() {
        if (Value <= 0f) return;
        if (layout.preferredWidth < 0f || layout.preferredWidth > Value) layout.preferredWidth = Value;
        if (rect.rect.width <= Value + .1f) return;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Value);
        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform.parent);
    }
}
