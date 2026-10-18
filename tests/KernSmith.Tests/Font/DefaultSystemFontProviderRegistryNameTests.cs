using KernSmith.Font;
using Shouldly;

namespace KernSmith.Tests.Font;

public class DefaultSystemFontProviderRegistryNameTests
{
    [Theory]
    [InlineData("Arial (TrueType)", "Arial")]
    [InlineData("Arial Bold Italic (OpenType)", "Arial Bold Italic")]
    [InlineData("Batang & BatangChe (TrueType)", "Batang & BatangChe")]
    [InlineData("Mana Seed Title (monospace) (TrueType)", "Mana Seed Title (monospace)")]
    [InlineData("Mana Seed Title (monospace) Bold (OpenType)", "Mana Seed Title (monospace) Bold")]
    [InlineData("Plain Name", "Plain Name")]
    public void StripRegistryTypeSuffix_RemovesOnlyTrailingTypeGroup(string registryName, string expected)
    {
        DefaultSystemFontProvider.StripRegistryTypeSuffix(registryName).ShouldBe(expected);
    }
}
