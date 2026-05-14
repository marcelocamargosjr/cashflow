using Cashflow.Ledger.Application.Entries.Dtos;
using Cashflow.SharedKernel.Results;
using MediatR;

namespace Cashflow.Ledger.Application.Entries.Commands.ReverseEntry;

public sealed record ReverseEntryCommand(
    Guid EntryId,
    Guid MerchantId,
    string Reason) : IRequest<Result<EntryDto>>;
