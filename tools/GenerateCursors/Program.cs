// Exports the current generated resize-cursor artwork as PNG assets. Edit the constants/shape
// below, then from this folder run:
//
//   dotnet run
//
// This writes the source PNGs under ../../assets/icons. Rebuild UITools afterwards to embed them.
//
// The shape is drawn at SuperSample times the final texture size, then box-filter downsampled
// (with alpha premultiplied before averaging, to avoid dark fringing where the shape meets the
// transparent background) - this gives smooth anti-aliased edges at a small on-screen size instead
// of the hard/jagged edges a per-pixel distance-field produces when drawn directly at display size.

using System;
using System.IO;
using System.IO.Compression;

// --- Tweak these to change how the cursor looks ---
const int SuperSize = 320; // the resolution the shape is actually drawn/generated at
const int SuperSample = 8; // downsample factor: final texture is SuperSize/SuperSample per side
// Shape dimensions as fractions of SuperSize rather than fixed pixel counts, so they scale
// automatically if SuperSize changes instead of needing to be hand-recomputed every time.
const float LengthFraction = 80f / 96f; // total arrow length (shaft + both heads)
const float ShaftHalfWidthFraction = 5.6f / 96f; // half-thickness of the straight shaft
const float HeadLengthFraction = 28f / 96f; // length of each triangular arrowhead
const float HeadHalfWidthFraction = 18f / 96f; // half-width of an arrowhead at its base
const float OutlineThicknessFraction = 4f / 96f; // how much the outline extends beyond the fill
// ----------------------------------------------------

const int Size = SuperSize / SuperSample; // final texture width/height in pixels (what's displayed)
const float Length = SuperSize * LengthFraction;
const float ShaftHalfWidth = SuperSize * ShaftHalfWidthFraction;
const float HeadLength = SuperSize * HeadLengthFraction;
const float HeadHalfWidth = SuperSize * HeadHalfWidthFraction;
const float OutlineThickness = SuperSize * OutlineThicknessFraction;

byte[] DrawDoubleArrowSupersampled(float degrees)
{
    float rad = degrees * MathF.PI / 180f;
    (float dx, float dy) dir = (MathF.Cos(rad), MathF.Sin(rad));
    (float x, float y) perp = (-dir.dy, dir.dx);
    (float x, float y) center = (SuperSize / 2f, SuperSize / 2f);

    var super = new byte[SuperSize * SuperSize * 4];

    for (int y = 0; y < SuperSize; y++)
    {
        for (int x = 0; x < SuperSize; x++)
        {
            float px = x + 0.5f - center.x;
            float py = y + 0.5f - center.y;
            float along = px * dir.dx + py * dir.dy;
            float across = px * perp.x + py * perp.y;
            float halfLen = Length / 2f;

            bool inShaft = MathF.Abs(across) <= ShaftHalfWidth && MathF.Abs(along) <= halfLen - HeadLength;
            float tipDist = halfLen - MathF.Abs(along);
            bool inHead = MathF.Abs(along) > halfLen - HeadLength && MathF.Abs(along) <= halfLen
                && MathF.Abs(across) <= HeadHalfWidth * (tipDist / HeadLength);

            bool onOutline = false;
            if (!inShaft && !inHead)
            {
                bool inShaftOutline = MathF.Abs(across) <= ShaftHalfWidth + OutlineThickness && MathF.Abs(along) <= halfLen - HeadLength;
                bool inHeadOutline = MathF.Abs(along) > halfLen - HeadLength - OutlineThickness && MathF.Abs(along) <= halfLen + OutlineThickness
                    && MathF.Abs(across) <= HeadHalfWidth * (tipDist / HeadLength) + OutlineThickness * 1.2f;
                onOutline = inShaftOutline || inHeadOutline;
            }

            byte r, g, b, a;
            if (inShaft || inHead) { r = g = b = 0; a = 255; }
            else if (onOutline) { r = g = b = 255; a = 255; }
            else { r = g = b = 0; a = 0; }

            int index = (y * SuperSize + x) * 4;
            super[index] = r;
            super[index + 1] = g;
            super[index + 2] = b;
            super[index + 3] = a;
        }
    }

    return Downsample(super);
}

byte[] Downsample(byte[] super)
{
    var final = new byte[Size * Size * 4];

    for (int oy = 0; oy < Size; oy++)
    {
        for (int ox = 0; ox < Size; ox++)
        {
            // Premultiply by alpha before averaging so a block that's half opaque-white,
            // half transparent-black doesn't average toward a dark gray fringe.
            float sumR = 0, sumG = 0, sumB = 0, sumA = 0;
            for (int sy = 0; sy < SuperSample; sy++)
            {
                for (int sx = 0; sx < SuperSample; sx++)
                {
                    int index = ((oy * SuperSample + sy) * SuperSize + (ox * SuperSample + sx)) * 4;
                    float a = super[index + 3] / 255f;
                    sumR += super[index] / 255f * a;
                    sumG += super[index + 1] / 255f * a;
                    sumB += super[index + 2] / 255f * a;
                    sumA += a;
                }
            }

            int sampleCount = SuperSample * SuperSample;
            float avgA = sumA / sampleCount;
            float avgR = avgA > 0f ? sumR / sumA : 0f;
            float avgG = avgA > 0f ? sumG / sumA : 0f;
            float avgB = avgA > 0f ? sumB / sumA : 0f;

            int outIndex = (oy * Size + ox) * 4;
            final[outIndex] = (byte)Math.Clamp(MathF.Round(avgR * 255f), 0, 255);
            final[outIndex + 1] = (byte)Math.Clamp(MathF.Round(avgG * 255f), 0, 255);
            final[outIndex + 2] = (byte)Math.Clamp(MathF.Round(avgB * 255f), 0, 255);
            final[outIndex + 3] = (byte)Math.Clamp(MathF.Round(avgA * 255f), 0, 255);
        }
    }

    return final;
}

byte[] ResizeForAsset(byte[] source)
{
    const int assetSize = 64;
    var output = new byte[assetSize * assetSize * 4];
    for (var y = 0; y < assetSize; y++)
    for (var x = 0; x < assetSize; x++)
    {
        // Mirrors the old Texture2D.GetPixelBilinear call used by UiIcons exactly enough for
        // these source pixels: Unity maps normalized UVs across the outer pixel centres.
        var sourceX = ((x + .5f) / assetSize) * (Size - 1);
        var sourceY = ((y + .5f) / assetSize) * (Size - 1);
        var x0 = Math.Clamp((int)MathF.Floor(sourceX), 0, Size - 1);
        var y0 = Math.Clamp((int)MathF.Floor(sourceY), 0, Size - 1);
        var x1 = Math.Min(x0 + 1, Size - 1);
        var y1 = Math.Min(y0 + 1, Size - 1);
        var tx = sourceX - x0;
        var ty = sourceY - y0;
        for (var channel = 0; channel < 4; channel++)
        {
            float Sample(int sx, int sy) => source[(sy * Size + sx) * 4 + channel];
            var top = Sample(x0, y0) + (Sample(x1, y0) - Sample(x0, y0)) * tx;
            var bottom = Sample(x0, y1) + (Sample(x1, y1) - Sample(x0, y1)) * tx;
            output[(y * assetSize + x) * 4 + channel] = (byte)Math.Clamp(MathF.Round(top + (bottom - top) * ty), 0, 255);
        }
    }
    return output;
}

byte[] DrawGizmoHandle()
{
    const int size = 64;
    var pixels = new byte[size * size * 4];
    var center = (size - 1) / 2f;
    for (var y = 0; y < size; y++)
    for (var x = 0; x < size; x++)
    {
        var dx = x - center;
        var dy = y - center;
        var radius = MathF.Sqrt(dx * dx + dy * dy) / (size / 2f);
        var alpha = radius <= .72f ? .92f : radius <= .94f ? 1f : 0f;
        var index = (y * size + x) * 4;
        pixels[index] = pixels[index + 1] = pixels[index + 2] = 255;
        pixels[index + 3] = (byte)MathF.Round(alpha * 255f);
    }
    return pixels;
}

uint Crc32(byte[] bytes)
{
    uint crc = 0xffffffff;
    foreach (var value in bytes)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
    }
    return ~crc;
}

void WritePng(string path, byte[] rgba, int width, int height)
{
    using var file = File.Create(path);
    file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    void Chunk(string name, byte[] data)
    {
        var type = System.Text.Encoding.ASCII.GetBytes(name);
        void UInt32(uint value) => file.Write(new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value });
        UInt32((uint)data.Length);
        file.Write(type);
        file.Write(data);
        var crcData = new byte[type.Length + data.Length];
        Buffer.BlockCopy(type, 0, crcData, 0, type.Length);
        Buffer.BlockCopy(data, 0, crcData, type.Length, data.Length);
        UInt32(Crc32(crcData));
    }
    var header = new byte[13];
    header[3] = (byte)width; header[7] = (byte)height; header[8] = 8; header[9] = 6;
    Chunk("IHDR", header);
    var scanlines = new byte[height * (width * 4 + 1)];
    for (var y = 0; y < height; y++)
    {
        Buffer.BlockCopy(rgba, y * width * 4, scanlines, y * (width * 4 + 1) + 1, width * 4);
    }
    using var compressed = new MemoryStream();
    using (var deflate = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true)) deflate.Write(scanlines);
    Chunk("IDAT", compressed.ToArray());
    Chunk("IEND", Array.Empty<byte>());
}

// ResizeCursorShape.<Name> -> arrow angle in degrees (0 = pointing east, CCW positive).
// Add/remove entries here if ResizeCursor.cs's enum changes.
var shapes = new (string Name, float Degrees)[]
{
    ("Horizontal", 0f),
    ("Vertical", 90f),
    ("DiagonalNeSw", 45f),
    ("DiagonalNwSe", 135f)
};

var outputDirectory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "assets", "icons"));
Directory.CreateDirectory(outputDirectory);
foreach (var (name, degrees) in shapes)
{
    var pixels = ResizeForAsset(DrawDoubleArrowSupersampled(degrees));
    var outputPath = Path.Combine(outputDirectory, $"cursor-{name}.png");
    WritePng(outputPath, pixels, 64, 64);
    Console.WriteLine($"Wrote {outputPath}");
}
var gizmoPath = Path.Combine(outputDirectory, "gizmo-handle.png");
WritePng(gizmoPath, DrawGizmoHandle(), 64, 64);
Console.WriteLine($"Wrote {gizmoPath}");
