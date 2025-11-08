using FluentValidation;
using AllSpice.CleanModularMonolith.SharedKernel.ValueObjects;

namespace AllSpice.CleanModularMonolith.SharedKernel.Validation;

public static class ValidationExtensions
{
    public static IRuleBuilderOptions<T, string> MustBeValidEmail<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder.Must(value =>
            {
                try
                {
                    _ = EmailAddress.Create(value);
                    return true;
                }
                catch
                {
                    return false;
                }
            })
            .WithMessage("'{PropertyName}' is not a valid email address.");

    public static IRuleBuilderOptions<T, string> MustBeValidPhoneNumber<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder.Must(value =>
            {
                try
                {
                    _ = PhoneNumber.Create(value);
                    return true;
                }
                catch
                {
                    return false;
                }
            })
            .WithMessage("'{PropertyName}' is not a valid phone number.");
}


