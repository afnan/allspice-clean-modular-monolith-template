using System.Security.Claims;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Claims;

namespace AllSpice.CleanModularMonolith.ApiGateway.UnitTests.Identity;

/// <summary>
/// D3: the external-subject fallback chain was duplicated (Web + AppHub) and had drifted (AppHub omitted
/// <c>client_id</c>). It now lives once in <see cref="ClaimsPrincipalExtensions.GetSubjectId"/>.
/// </summary>
public class GetSubjectIdTests
{
    private static ClaimsPrincipal PrincipalWith(params (string Type, string Value)[] claims)
        => new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), authenticationType: "test"));

    [Fact]
    public void Prefers_name_identifier()
    {
        var principal = PrincipalWith(
            (ClaimTypes.NameIdentifier, "nameid"),
            ("sub", "sub-value"));

        Assert.Equal("nameid", principal.GetSubjectId());
    }

    [Theory]
    [InlineData("sub")]
    [InlineData("oid")]
    [InlineData("client_id")]
    public void Falls_back_through_the_chain(string claimType)
    {
        var principal = PrincipalWith((claimType, "resolved"));

        Assert.Equal("resolved", principal.GetSubjectId());
    }

    [Fact]
    public void Returns_null_when_no_identifier_claim()
    {
        var principal = PrincipalWith(("unrelated", "x"));

        Assert.Null(principal.GetSubjectId());
    }
}
