using AllSpice.CleanModularMonolith.Identity.Application.Features.Invitations.Commands.InviteUser;

namespace AllSpice.CleanModularMonolith.Identity.Domain.UnitTests;

public class TempPasswordGeneratorTests
{
    [Fact]
    public void Generate_always_includes_each_required_character_class()
    {
        const string special = "!@#$%";

        // Run many iterations: a uniform draw could occasionally omit a class, which is the bug being fixed.
        for (var i = 0; i < 500; i++)
        {
            var password = TempPasswordGenerator.Generate();

            Assert.Equal(TempPasswordGenerator.Length, password.Length);
            Assert.Contains(password, char.IsLower);
            Assert.Contains(password, char.IsUpper);
            Assert.Contains(password, char.IsDigit);
            Assert.Contains(password, c => special.Contains(c));
        }
    }
}
