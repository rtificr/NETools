using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace NETools;

/// <summary>
/// Shared NETools image assets. Plugins should consume these sprites/textures instead of creating
/// their own cursor or gizmo artwork, keeping the mod ecosystem visually consistent.
/// </summary>
public static class UiIcons {
    private static readonly Dictionary<ResizeCursorShape, Texture2D> CursorTextures = new();
    private static readonly Dictionary<ResizeCursorShape, Sprite> CursorSprites = new();
    private static Sprite gizmoHandle;
    private static readonly MethodInfo UnityLoadImage = FindUnityPngLoader();

    public static Texture2D ResizeCursor(ResizeCursorShape shape) {
        if (CursorTextures.TryGetValue(shape, out var existing)) return existing;
        var texture = LoadTexture($"cursor-{shape}.png");
        CursorTextures[shape] = texture;
        return texture;
    }

    /// <summary>Shared high-resolution Move cursor texture.</summary>
    public static Texture2D MoveCursor => ResizeCursor(ResizeCursorShape.Move);

    /// <summary>Full-resolution cursor artwork for NETools' screen-space cursor visual.</summary>
    public static Sprite ResizeCursorSprite(ResizeCursorShape shape) {
        if (CursorSprites.TryGetValue(shape, out var existing)) return existing;
        var texture = ResizeCursor(shape);
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), texture.width);
        CursorSprites[shape] = sprite;
        return sprite;
    }

    /// <summary>Shared high-resolution Move cursor sprite for UI-based cursor visuals.</summary>
    public static Sprite MoveCursorSprite => ResizeCursorSprite(ResizeCursorShape.Move);

    /// <summary>A circular Sprite suitable for a Unity UI Image on draggable gizmo handles.</summary>
    public static Sprite GizmoHandle => gizmoHandle ??= Sprite.Create(
        LoadTexture("gizmo-handle.png"), new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 64);

    private static Texture2D LoadTexture(string fileName) {
        var resourceName = $"NETools.Assets.Icons.{fileName}";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream == null) throw new InvalidOperationException($"Missing embedded NETools image: {resourceName}");
        var bytes = new byte[stream.Length];
        var offset = 0;
        while (offset < bytes.Length) {
            var read = stream.Read(bytes, offset, bytes.Length - offset);
            if (read == 0) throw new EndOfStreamException($"Could not read embedded NETools image: {resourceName}");
            offset += read;
        }
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var decoded = (bool)UnityLoadImage.Invoke(null, new object[] { texture, bytes, false });
        if (!decoded) throw new InvalidOperationException($"Could not decode embedded NETools PNG: {resourceName}");
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }

    private static MethodInfo FindUnityPngLoader() {
        var imageConversion = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
        var loader = imageConversion?.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
        if (loader == null) throw new MissingMethodException("Unity's ImageConversion.LoadImage(Texture2D, byte[], bool) is unavailable.");
        return loader;
    }

}
