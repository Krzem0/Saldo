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

        RuleFor(command => command.PayerId)
            .GreaterThan(0).When(command => command.PayerId.HasValue)
            .WithErrorCode(ErrorCodes.Transaction.PayerInvalid);

        RuleFor(command => command.CounterpartyId)
            .GreaterThan(0).When(command => command.CounterpartyId.HasValue)
            .WithErrorCode(ErrorCodes.Transaction.CounterpartyInvalid);

        RuleFor(command => command.Description)
            .NotEmpty().WithErrorCode(ErrorCodes.Transaction.DescriptionRequired)
            .MaximumLength(500).WithErrorCode(ErrorCodes.Transaction.DescriptionTooLong);

        RuleFor(command => command.Date)
            .NotEmpty().WithErrorCode(ErrorCodes.Transaction.DateRequired);

        RuleFor(command => command.Type)
            .IsInEnum().WithErrorCode(ErrorCodes.Transaction.TypeInvalid);
    }
}
