using AllSpice.CleanModularMonolith.SharedKernel.ValueObjects;

namespace AllSpice.CleanModularMonolith.SharedKernel.UnitTests.ValueObjects;

public class EmailAddressTests
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("USER@Example.org")]
    public void Create_ReturnsValueObject_WhenFormatValid(string input)
    {
        var email = EmailAddress.Create(input);

        Assert.Equal(input.Trim(), email.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-email")]
    public void Create_Throws_WhenInvalid(string? input)
    {
        Assert.ThrowsAny<ArgumentException>(() => EmailAddress.Create(input!));
    }
}


