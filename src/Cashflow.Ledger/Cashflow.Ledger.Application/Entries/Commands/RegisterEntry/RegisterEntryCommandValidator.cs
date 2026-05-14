using Cashflow.Ledger.Domain.Entries;
using FluentValidation;

namespace Cashflow.Ledger.Application.Entries.Commands.RegisterEntry;

internal sealed class RegisterEntryCommandValidator : AbstractValidator<RegisterEntryCommand>
{
    public RegisterEntryCommandValidator()
    {
        RuleFor(x => x.MerchantId).NotEqual(Guid.Empty);
        RuleFor(x => x.IdempotencyKey).NotEqual(Guid.Empty);
        RuleFor(x => x.Type).IsInEnum().NotEqual(default(EntryType));
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Must(c => string.Equals(c, "BRL", StringComparison.Ordinal))
            .WithMessage("Currency must be 'BRL'");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category).MaximumLength(50);
    }
}
