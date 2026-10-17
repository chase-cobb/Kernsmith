using System.Buffers;
using KernSmith.Atlas;
using KernSmith.Font.Models;

namespace KernSmith.Rasterizer;

/// <summary>
/// Generates the outline layer via a Euclidean Distance Transform: an anti-aliased ring
/// around the glyph on both the exterior and the interior counter walls, plus a solid
/// (full-opacity) backing beneath the glyph body so the body composites on top without a seam.
/// Z-order 1: renders behind the body, in front of the shadow.
/// </summary>
internal sealed class OutlineEffect : IGlyphEffect
{
    public int ZOrder => 1;

    private readonly int _outlineWidth;
    private readonly byte _outlineR;
    private readonly byte _outlineG;
    private readonly byte _outlineB;

    private readonly bool _smooth;

    /// <summary>
    /// Creates the effect. <paramref name="smooth"/> true gives the soft anti-aliased ring; false
    /// (AntiAlias = None) gives a hard threshold: pixels within <paramref name="outlineWidth"/> of
    /// the glyph are fully opaque, the rest transparent.
    /// </summary>
    public OutlineEffect(int outlineWidth, byte outlineR = 0, byte outlineG = 0, byte outlineB = 0, bool smooth = true)
    {
        _outlineWidth = outlineWidth;
        _outlineR = outlineR;
        _outlineG = outlineG;
        _outlineB = outlineB;
        _smooth = smooth;
    }

    public GlyphLayer Generate(byte[] alphaData, int width, int height, int pitch, GlyphMetrics metrics)
    {
        var ow = _outlineWidth;
        var dstW = width + 2 * ow;
        var dstH = height + 2 * ow;
        var size = dstW * dstH;

        // Step 1: Extract source alpha into an expanded buffer, centered.
        // expandedAlpha is read in full later (border pixels are read but never written),
        // so a rented buffer must be cleared first.
        var expandedAlpha = ArrayPool<byte>.Shared.Rent(size);
        var binaryAlpha = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            Array.Clear(expandedAlpha, 0, size);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var srcIdx = y * pitch + x;
                    var alpha = srcIdx < alphaData.Length ? alphaData[srcIdx] : (byte)0;
                    expandedAlpha[(y + ow) * dstW + (x + ow)] = alpha;
                }
            }

            // Step 2: Binarize alpha so the EDT measures distance from the opaque glyph core.
            // Semi-transparent antialiased edge pixels (alpha < 128) are treated as "outside",
            // which lets the outline extend under the fringe and eliminates edge gaps.
            for (var i = 0; i < size; i++)
                binaryAlpha[i] = expandedAlpha[i] >= 32 ? (byte)255 : (byte)0;

            var squaredDist = EuclideanDistanceTransform.Compute(binaryAlpha, dstW, dstH);

            // Step 3: Build RGBA output with outline color and anti-aliased alpha.
            // Two cases per pixel:
            //   - Body (under the glyph fill): full-opacity backing so alpha-over compositing
            //     has no seam behind the body's anti-aliased edge.
            //   - Everything else (exterior ring AND interior counters): a distance-based
            //     anti-aliased ring. The falloff fades to zero beyond ~ow pixels from the
            //     glyph core, so the open center of a large counter stays transparent on its
            //     own -- no flood-fill needed -- while pixels near an inner stroke wall get
            //     a proper inner outline.
            var dst = new byte[size * 4];

            for (var y = 0; y < dstH; y++)
            {
                for (var x = 0; x < dstW; x++)
                {
                    var pixelIdx = y * dstW + x;
                    var hasSourceAlpha = expandedAlpha[pixelIdx] > 0;

                    if (hasSourceAlpha)
                    {
                        // Body area: fill with full outline alpha so there's no seam
                        // when the body composites on top.
                        var idx = pixelIdx * 4;
                        dst[idx + 0] = _outlineR;
                        dst[idx + 1] = _outlineG;
                        dst[idx + 2] = _outlineB;
                        dst[idx + 3] = 255;
                    }
                    else
                    {
                        // Exterior or counter: distance-based anti-aliased ring.
                        float outlineAlpha;
                        if (_smooth)
                        {
                            var dist = MathF.Sqrt(squaredDist[pixelIdx]);
                            // Smooth falloff: fully opaque up to ow, then linear fade over 1.5 pixels.
                            outlineAlpha = Math.Clamp(255f * (ow + 0.75f - dist) / 1.5f, 0f, 255f);
                        }
                        else
                        {
                            // No anti-aliasing: hard threshold so Outline = N is exactly N solid pixels.
                            outlineAlpha = squaredDist[pixelIdx] <= (float)ow * ow ? 255f : 0f;
                        }

                        if (outlineAlpha <= 0)
                            continue;

                        var idx = pixelIdx * 4;
                        dst[idx + 0] = _outlineR;
                        dst[idx + 1] = _outlineG;
                        dst[idx + 2] = _outlineB;
                        dst[idx + 3] = (byte)outlineAlpha;
                    }
                }
            }

            return new GlyphLayer(dst, dstW, dstH, OffsetX: -ow, OffsetY: -ow, ZOrder);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(expandedAlpha);
            ArrayPool<byte>.Shared.Return(binaryAlpha);
        }
    }
}
