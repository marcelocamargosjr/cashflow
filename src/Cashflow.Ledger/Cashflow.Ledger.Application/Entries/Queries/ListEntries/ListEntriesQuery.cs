using Cashflow.Ledger.Application.Entries.Dtos;
using Cashflow.Ledger.Domain.Entries;
using Cashflow.SharedKernel.Results;
using MediatR;

namespace Cashflow.Ledger.Application.Entries.Queries.ListEntries;

public sealed record ListEntriesQuery(
    Guid MerchantId,
    DateOnly From,
    DateOnly To,
    EntryType? Type,
    string? Category,
    int Page,
    int Size) : IRequest<Result<PagedResultDto<EntryDto>>>;
