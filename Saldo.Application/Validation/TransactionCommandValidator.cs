using FluentValidation;
using Saldo.Application.DTOs;
using Saldo.Application.Errors;

namespace Saldo.Application.Validation;

public abstract class TransactionCommandValidator<TCommand> : AbstractValidator<TCommand>
    where TCommand : ITransactionCommand
{
    protected TransactionCommandValidator()
    {
        RuleFor(command => command.Amount)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.Transaction.AmountMustBePositive);

        RuleFor(command => command.CategoryId)
            .GreaterThan(0)
            .WithErrorCode(ErrorCodes.Transaction.CategoryRequired);

        RuleFor(command => command.PayerName)
            .NotEmpty()
            .When(command => !command.PayerId.HasValue || command.PayerId.Value <= 0)
            .WithErrorCode(ErrorCodes.Transaction.PayerRequired);

        RuleFor(command => command.CounterpartyName)
            .NotEmpty()
            .When(command => !command.CounterpartyId.HasValue || command.CounterpartyId.Value <= 0)
            .WithErrorCode(ErrorCodes.Transaction.CounterpartyRequired);
    }
}
