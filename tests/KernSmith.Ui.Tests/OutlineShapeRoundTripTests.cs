using KernSmith.Ui.Services;
using KernSmith.Ui.ViewModels;
using Shouldly;

namespace KernSmith.Ui.Tests;

public sealed class OutlineShapeRoundTripTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("kernsmith-ui-tests").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { /* best effort */ }
    }

    private string WriteConfig(string name, string extraKeys)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "fileVersion=1\nfontName=Arial\nfontSize=32\nchars=32-126\n" + extraKeys);
        return path;
    }

    private static (FontConfigViewModel, AtlasConfigViewModel, EffectsViewModel, CharacterGridViewModel) NewViewModels()
        => (new FontConfigViewModel(), new AtlasConfigViewModel(), new EffectsViewModel(), new CharacterGridViewModel());

    [Theory]
    [InlineData(OutlineShape.Square)]
    [InlineData(OutlineShape.Plus)]
    public void LoadThenSave_PreservesOutlineShape(OutlineShape shape)
    {
        var service = new ProjectService();
        var (fontConfig, atlasConfig, effects, characterGrid) = NewViewModels();
        var source = WriteConfig("source.bmfc", $"outlineThickness=2\noutlineShape={shape.ToString().ToLowerInvariant()}\n");

        service.LoadProject(source, fontConfig, atlasConfig, effects, characterGrid);
        effects.OutlineShape.ShouldBe(shape);

        var resaved = Path.Combine(_dir, "resaved.bmfc");
        service.SaveProject(resaved, fontConfig, atlasConfig, effects, characterGrid);

        BmfcConfigReader.Read(resaved).Options.OutlineShape.ShouldBe(shape);
    }

    [Fact]
    public void LoadProject_WithoutOutlineShape_DefaultsToRound()
    {
        var service = new ProjectService();
        var (fontConfig, atlasConfig, effects, characterGrid) = NewViewModels();

        service.LoadProject(WriteConfig("plain.bmfc", "outlineThickness=2\n"), fontConfig, atlasConfig, effects, characterGrid);

        effects.OutlineShape.ShouldBe(OutlineShape.Round);
    }
}
