using System.Reflection;
using System.Runtime.InteropServices;
using KernSmith.Font;
using KernSmith.Output;
using KernSmith.Rasterizer;
using KernSmith.Rasterizers.FreeType;
using KernSmith.Rasterizers.Native;
using KernSmith.Rasterizers.StbTrueType;

namespace KernSmith.PlatformSmoke;

/// <summary>
/// Runs the platform smoke checks and reports each as a "KSMOKE PASS|FAIL|SKIP" line.
/// The final line is "KSMOKE RESULT PASS" or "KSMOKE RESULT FAIL"; CI greps for it.
/// </summary>
public static class SmokeRunner
{
    public const string Prefix = "KSMOKE";

    /// <summary>Runs every check. Returns true when nothing failed.</summary>
    public static bool Run(Action<string> log)
    {
        int failures = 0;

        void Report(string status, string name, string detail = "")
        {
            if (status == "FAIL") failures++;
            log($"{Prefix} {status} {name}{(detail.Length > 0 ? " -- " + detail : "")}");
        }

        void Check(string name, Func<string> body)
        {
            try
            {
                Report("PASS", name, body());
            }
            catch (SkipException ex)
            {
                Report("SKIP", name, ex.Message);
            }
            catch (Exception ex)
            {
                Report("FAIL", name, $"{ex.GetType().Name}: {ex.Message}");
                log(ex.ToString());
            }
        }

        log($"{Prefix} INFO os={RuntimeInformation.OSDescription} rid={RuntimeInformation.RuntimeIdentifier} " +
            $"arch={RuntimeInformation.ProcessArchitecture} framework={RuntimeInformation.FrameworkDescription} " +
            $"dynamicCode={System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported}");

        // Explicit registration: the documented pattern for trimmed/AOT apps, where
        // reflection-based backend auto-discovery is not available.
        RasterizerFactory.Register(RasterizerBackend.StbTrueType, () => new StbTrueTypeRasterizer());
        RasterizerFactory.Register(RasterizerBackend.FreeType, () => new FreeTypeRasterizer());
        RasterizerFactory.Register(RasterizerBackend.Native, () => new NativeRasterizer());

        var roboto = LoadFont("Roboto-Regular.ttf");
        var robotoFlex = LoadFont("RobotoFlex-Variable.ttf");

        Check("SystemFontProvider does not throw", () =>
        {
            var fonts = new DefaultSystemFontProvider().GetInstalledFonts();
            return $"{fonts.Count} installed fonts";
        });

        foreach (var backend in new[] { RasterizerBackend.StbTrueType, RasterizerBackend.FreeType, RasterizerBackend.Native })
        {
            IRasterizerCapabilities? caps = null;
            Check($"{backend} create", () =>
            {
                using var rasterizer = RasterizerFactory.Create(backend);
                caps = rasterizer.Capabilities;
                return $"sdf={caps.SupportsSdf} outline={caps.SupportsOutlineStroke} variable={caps.SupportsVariableFonts}";
            });
            if (caps is null)
                continue;

            Check($"{backend} generate", () => Verify(BmFont.Generate(roboto, new FontGeneratorOptions
            {
                Size = 32,
                Characters = CharacterSet.Ascii,
                Backend = backend,
            }), expectKerning: true));

            Check($"{backend} outline", () =>
            {
                if (!caps.SupportsOutlineStroke) throw new SkipException("backend has no outline stroke");
                return Verify(BmFont.Generate(roboto, new FontGeneratorOptions
                {
                    Size = 32,
                    Characters = CharacterSet.Ascii,
                    Backend = backend,
                    Outline = 2,
                }), expectKerning: true);
            });

            Check($"{backend} sdf", () =>
            {
                if (!caps.SupportsSdf) throw new SkipException("backend has no SDF");
                return Verify(BmFont.Generate(roboto, new FontGeneratorOptions
                {
                    Size = 32,
                    Characters = CharacterSet.Ascii,
                    Backend = backend,
                    Sdf = true,
                }), expectKerning: true);
            });

            Check($"{backend} variable font", () =>
            {
                if (!caps.SupportsVariableFonts) throw new SkipException("backend has no variable font support");
                return Verify(BmFont.Generate(robotoFlex, new FontGeneratorOptions
                {
                    Size = 32,
                    Characters = CharacterSet.Ascii,
                    Backend = backend,
                    VariationAxes = new Dictionary<string, float> { ["wght"] = 700 },
                }), expectKerning: false);
            });
        }

        log($"{Prefix} RESULT {(failures == 0 ? "PASS" : "FAIL")} ({failures} failed)");
        return failures == 0;
    }

    private static string Verify(BmFontResult result, bool expectKerning)
    {
        int chars = result.Model.Characters.Count;
        if (chars < 95)
            throw new InvalidOperationException($"expected at least 95 chars but got {chars}");
        if (expectKerning && result.Model.KerningPairs.Count == 0)
            throw new InvalidOperationException("expected kerning pairs but got none");
        if (result.Pages.Count == 0)
            throw new InvalidOperationException("no atlas pages");

        var rgba = result.Pages[0].GetRgbaPixelData();
        int inked = 0;
        for (int i = 3; i < rgba.Length; i += 4)
            if (rgba[i] != 0) inked++;
        if (inked == 0)
            throw new InvalidOperationException("atlas page 0 is fully transparent");

        var png = result.GetPngData(0);
        if (png.Length < 8 || png[0] != 0x89 || png[1] != (byte)'P' || png[2] != (byte)'N' || png[3] != (byte)'G')
            throw new InvalidOperationException("page 0 PNG has no PNG signature");
        if (!result.FntText.Contains("chars count=", StringComparison.Ordinal))
            throw new InvalidOperationException(".fnt text has no chars line");

        return $"chars={chars} kerning={result.Model.KerningPairs.Count} pages={result.Pages.Count} inkedPixels={inked}";
    }

    private static byte[] LoadFont(string name)
    {
        using var stream = typeof(SmokeRunner).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"embedded font {name} not found");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private sealed class SkipException(string message) : Exception(message);
}
