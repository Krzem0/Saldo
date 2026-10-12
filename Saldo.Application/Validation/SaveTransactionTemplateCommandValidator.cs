using FluentValidation;
using Saldo.Application.DTOs;

namespace Saldo.Application.Validation;

public sealed class SaveTransactionTemplateCommandValidator : AbstractValidator<SaveTransactionTemplateCommand>
{
    public SaveTransactionTemplateCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithErrorCode("TransactionTemplate.NameRequired")
            .MaximumLength(100).WithErrorCode("TransactionTemplate.NameTooLong");
        RuleFor(x => x.Description).MaximumLength(500).WithErrorCode("TransactionTemplate.DescriptionTooLong");
        RuleFor(x => x.TransactionType).IsInEnum().WithErrorCode("TransactionTemplate.TypeInvalid");
        RuleFor(x => x.Amount).GreaterThan(0).When(x => x.Amount.HasValue)
            .WithErrorCode("TransactionTemplate.AmountMustBePositive");
        RuleFor(x => x.CategoryId).GreaterThan(0).When(x => x.CategoryId.HasValue).WithErrorCode("TransactionTemplate.CategoryInvalid");
        RuleFor(x => x.PayerId).GreaterThan(0).When(x => x.PayerId.HasValue).WithErrorCode("TransactionTemplate.PayerInvalid");
        RuleFor(x => x.CounterpartyId).GreaterThan(0).When(x => x.CounterpartyId.HasValue).WithErrorCode("TransactionTemplate.CounterpartyInvalid");
        RuleFor(x => x.LocationId).GreaterThan(0).When(x => x.LocationId.HasValue).WithErrorCode("TransactionTemplate.LocationInvalid");
        RuleForEach(x => x.TagIds).GreaterThan(0).WithErrorCode("TransactionTemplate.TagInvalid");
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Description) || x.Amount.HasValue
                || x.CategoryId.HasValue || x.PayerId.HasValue || x.CounterpartyId.HasValue
                || x.LocationId.HasValue || x.TagIds is { Count: > 0 })
            .WithErrorCode("TransactionTemplate.ContentRequired");
    }
}
