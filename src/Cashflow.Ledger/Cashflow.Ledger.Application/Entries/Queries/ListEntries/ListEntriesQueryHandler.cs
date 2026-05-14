using Cashflow.Ledger.Application.Entries.Dtos;
using Cashflow.Ledger.Domain.Entries;
using Cashflow.SharedKernel.Results;
using MediatR;

namespace Cashflow.Ledger.Application.Entries.Queries.ListEntries;

internal sealed class ListEntriesQueryHandler(IEntryRepository entryRepository)
    : IRequestHandler<ListEntriesQuery, Result<PagedResultDto<EntryDto>>>
{
    private readonly IEntryRepository _entryRepository = entryRepository;

    public Task<Result<PagedResultDto<EntryDto>>> Handle(
        ListEntriesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _entryRepository.Query()
            .Where(e => e.MerchantId == request.MerchantId)
            .Where(e => e.EntryDate >= request.From && e.EntryDate <= request.To);

        if (request.Type is { } type)
            query = query.Where(e => e.Type == type);

        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(e => e.Category == request.Category);

        var total = query.Count();
        var items = query
            .OrderByDescending(e => e.EntryDate)
            .ThenByDescending(e => e.CreatedAt)
            .Skip((request.Page - 1) * request.Size)
            .Take(request.Size)
            .AsEnumerable()
            .Select(EntryDto.FromEntity)
            .ToList();

        var hasNext = request.Page * request.Size < total;

        Result<PagedResultDto<EntryDto>> result = new PagedResultDto<EntryDto>(
            items,
            request.Page,
            request.Size,
            total,
            hasNext);

        return Task.FromResult(result);
    }
}
