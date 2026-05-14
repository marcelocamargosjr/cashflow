using FluentValidation;

namespace Cashflow.Ledger.Application.Entries.Queries.GetEntry;

public sealed class GetEntryQueryValidator : AbstractValidator<GetEntryQuery>
{
    public GetEntryQueryValidator()
    {
        RuleFor(x => x.EntryId).NotEqual(Guid.Empty);
        RuleFor(x => x.MerchantId).NotEqual(Guid.Empty);
    }
}
