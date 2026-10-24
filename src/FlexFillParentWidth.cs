using UnityEngine;
using UnityEngine.UI;

namespace NETools;

/// <summary>Keeps a retained flex container's preferred width synchronized with its resizable parent.</summary>
internal sealed class FlexFillParentWidth : MonoBehaviour {
    private LayoutElement layout;
    private RectTransform parentRect;
    private float lastWidth = -1f;

    private void Awake() {
        layout = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
        parentRect = transform.parent as RectTransform;
    }

    private void LateUpdate() {
        if (layout == null || parentRect == null) return;
        var width = Mathf.Max(1f, parentRect.rect.width);
        if (Mathf.Abs(width - lastWidth) < .1f) return;
        lastWidth = width;
        layout.preferredWidth = width;
        LayoutRebuilder.MarkLayoutForRebuild(parentRect);
    }
}
