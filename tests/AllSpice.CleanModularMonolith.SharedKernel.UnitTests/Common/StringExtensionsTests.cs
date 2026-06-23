using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.SharedKernel.UnitTests.Common;

public class StringExtensionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Truncate_NullOrEmpty_ReturnsEmpty(string? value)
    {
        Assert.Equal(string.Empty, value.Truncate(10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Truncate_NonPositiveMaxLength_ReturnsEmpty(int maxLength)
    {
        Assert.Equal(string.Empty, "hello".Truncate(maxLength));
    }

    [Fact]
    public void Truncate_ShorterThanMax_ReturnsOriginal()
    {
        Assert.Equal("hello", "hello".Truncate(10));
    }

    [Fact]
    public void Truncate_ExactLength_ReturnsOriginal()
    {
        Assert.Equal("hello", "hello".Truncate(5));
    }

    [Fact]
    public void Truncate_LongerThanMax_CutsToMaxLength()
    {
        Assert.Equal("hel", "hello".Truncate(3));
    }

    [Fact]
    public void EscapeLikePattern_Null_Throws()
    {
        string value = null!;
        Assert.Throws<ArgumentNullException>(() => value.EscapeLikePattern());
    }

    [Theory]
    [InlineData("plainemail@example.com", "plainemail@example.com")]
    [InlineData("100%", "100\\%")]
    [InlineData("a_b", "a\\_b")]
    [InlineData("a\\b", "a\\\\b")]
    public void EscapeLikePattern_EscapesWildcards(string input, string expected)
    {
        Assert.Equal(expected, input.EscapeLikePattern());
    }

    [Fact]
    public void EscapeLikePattern_EscapesBackslashFirst_SoUnderscoreIsNotDoubleEscaped()
    {
        // Backslash must be escaped before the wildcard chars, otherwise "_" -> "\_"
        // would then have its new backslash doubled to "\\_". Verify the correct order.
        Assert.Equal("\\_", "_".EscapeLikePattern());
        Assert.Equal("\\%\\_", "%_".EscapeLikePattern());
    }
}
