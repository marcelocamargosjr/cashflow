using Cashflow.Ledger.Application.Entries.Dtos;
using Cashflow.SharedKernel.Results;
using MediatR;

namespace Cashflow.Ledger.Application.Entries.Queries.GetEntry;

public sealed record GetEntryQuery(Guid EntryId, Guid MerchantId) : IRequest<Result<EntryDto>>;
