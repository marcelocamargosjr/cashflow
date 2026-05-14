using FluentValidation;

namespace Cashflow.Ledger.Application.Entries.Commands.ReverseEntry;

public sealed class ReverseEntryCommandValidator : AbstractValidator<ReverseEntryCommand>
{
    public ReverseEntryCommandValidator()
    {
        RuleFor(x => x.EntryId).NotEqual(Guid.Empty);
        RuleFor(x => x.MerchantId).NotEqual(Guid.Empty);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
