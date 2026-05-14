using Cashflow.Ledger.Application.Entries.Dtos;
using Cashflow.Ledger.Domain.Entries;
using Cashflow.SharedKernel.Results;
using MediatR;

namespace Cashflow.Ledger.Application.Entries.Queries.GetEntry;

internal sealed class GetEntryQueryHandler(IEntryRepository entryRepository)
    : IRequestHandler<GetEntryQuery, Result<EntryDto>>
{
    private readonly IEntryRepository _entryRepository = entryRepository;

    public async Task<Result<EntryDto>> Handle(
        GetEntryQuery request,
        CancellationToken cancellationToken)
    {
        var entry = await _entryRepository
            .GetByIdAsync(request.EntryId, cancellationToken)
            .ConfigureAwait(false);

        if (entry is null || entry.MerchantId != request.MerchantId)
            return LedgerErrors.EntryNotFound;

        return EntryDto.FromEntity(entry);
    }
}
