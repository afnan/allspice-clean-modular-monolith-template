using Ardalis.Result;

namespace AllSpice.CleanModularMonolith.SharedKernel.Results;

public static class ResultExtensions
{
    public static IDictionary<string, string[]> ToValidationDictionary(this Result result)
    {
        if (result.Status != ResultStatus.Invalid)
        {
            return new Dictionary<string, string[]>();
        }

        return ToDictionary(result.ValidationErrors);
    }

    public static IDictionary<string, string[]> ToValidationDictionary<T>(this Result<T> result)
    {
        if (result.Status != ResultStatus.Invalid)
        {
            return new Dictionary<string, string[]>();
        }

        return ToDictionary(result.ValidationErrors);
    }

    private static IDictionary<string, string[]> ToDictionary(IEnumerable<ValidationError> validationErrors)
        => validationErrors
            .GroupBy(error => error.Identifier ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.Select(e => e.ErrorMessage).Distinct().ToArray());
}


