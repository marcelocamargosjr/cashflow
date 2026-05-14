using FluentValidation;

namespace Cashflow.Ledger.Application.Entries.Queries.ListEntries;

internal sealed class ListEntriesQueryValidator : AbstractValidator<ListEntriesQuery>
{
    public ListEntriesQueryValidator()
    {
        RuleFor(x => x.MerchantId).NotEqual(Guid.Empty);
        RuleFor(x => x.From).LessThanOrEqualTo(x => x.To);
        RuleFor(x => x)
            .Must(x => x.To.DayNumber - x.From.DayNumber <= 90)
            .WithMessage("Range from..to cannot exceed 90 days");
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.Size).InclusiveBetween(1, 200);
    }
}
