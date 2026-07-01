using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

namespace AllSpice.CleanModularMonolith.SharedKernel.UnitTests.Exceptions;

/// <summary>
/// Verifies that <see cref="DomainException.Code"/> produces correct snake_case codes,
/// including collapsing acronym runs (e.g. "API" → "api", not "a_p_i").
/// </summary>
public class DomainExceptionCodeTests
{
    // Concrete subclasses used only as Code-generation fixtures.
    private sealed class BusinessRuleViolationLikeException : DomainException
    {
        public BusinessRuleViolationLikeException() : base("test") { }
    }

    private sealed class APIValidationException : DomainException
    {
        public APIValidationException() : base("test") { }
    }

    private sealed class HttpNotFoundException : DomainException
    {
        public HttpNotFoundException() : base("test") { }
    }

    private sealed class XMLParseErrorException : DomainException
    {
        public XMLParseErrorException() : base("test") { }
    }

    [Fact]
    public void Code_SimpleWordBoundaries_ProducesSnakeCase()
    {
        // BusinessRuleViolationLikeException → "business_rule_violation_like"
        Assert.Equal("business_rule_violation_like", new BusinessRuleViolationLikeException().Code);
    }

    [Fact]
    public void Code_ExistingBuiltIn_BusinessRuleViolation_IsUnchanged()
    {
        // Verify the existing well-known code is stable after the acronym fix.
        Assert.Equal("business_rule_violation", new BusinessRuleViolationException("x").Code);
    }

    [Fact]
    public void Code_AcronymAtStart_CollapsesRun()
    {
        // APIValidationException → "api_validation" (not "a_p_i_validation")
        Assert.Equal("api_validation", new APIValidationException().Code);
    }

    [Fact]
    public void Code_AcronymMidWord_CollapsesRun()
    {
        // HttpNotFoundException → "http_not_found"
        Assert.Equal("http_not_found", new HttpNotFoundException().Code);
    }

    [Fact]
    public void Code_AcronymFollowedByAcronym_CollapsesCorrectly()
    {
        // XMLParseErrorException → "xml_parse_error"
        Assert.Equal("xml_parse_error", new XMLParseErrorException().Code);
    }
}
