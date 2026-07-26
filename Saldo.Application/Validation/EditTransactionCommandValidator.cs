using FluentValidation;
using Saldo.Application.DTOs;
using Saldo.Application.Errors;

namespace Saldo.Application.Validation;

public sealed class EditTransactionCommandValidator : TransactionCommandValidator<EditTransactionCommand>
{
    public EditTransactionCommandValidator()
    {
        RuleFor(command => command.Id)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.Transaction.IdMustBePositive);
    }
}
