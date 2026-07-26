using FluentResults;
using FluentValidation.Results;

namespace Saldo.Application.Validation;

internal static class ValidationResultExtensions
{
    public static Result<T> ToFailedResult<T>(this ValidationResult validationResult)
    {
        var errors = validationResult.Errors.Select(failure =>
            new Error(failure.ErrorCode)
                .WithMetadata("PropertyName", failure.PropertyName));

        return Result.Fail<T>(errors);
    }
}
