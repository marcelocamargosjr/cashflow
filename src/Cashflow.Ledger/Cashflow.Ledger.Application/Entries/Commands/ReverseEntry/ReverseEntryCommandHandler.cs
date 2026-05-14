using Cashflow.Ledger.Application.Entries.Dtos;
using Cashflow.Ledger.Domain.Abstractions;
using Cashflow.Ledger.Domain.Entries;
using Cashflow.SharedKernel.Domain;
using Cashflow.SharedKernel.Results;
using Cashflow.SharedKernel.Time;
using MediatR;

namespace Cashflow.Ledger.Application.Entries.Commands.ReverseEntry;

internal sealed class ReverseEntryCommandHandler(
    IEntryRepository entryRepository,
    IUnitOfWork unitOfWork,
    IClock clock)
    : IRequestHandler<ReverseEntryCommand, Result<EntryDto>>
{
    private readonly IEntryRepository _entryRepository = entryRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IClock _clock = clock;

    public async Task<Result<EntryDto>> Handle(
        ReverseEntryCommand request,
        CancellationToken cancellationToken)
    {
        var entry = await _entryRepository
            .GetByIdAsync(request.EntryId, cancellationToken)
            .ConfigureAwait(false);

        if (entry is null || entry.MerchantId != request.MerchantId)
            return LedgerErrors.EntryNotFound;

        if (entry.Status == EntryStatus.Reversed)
            return LedgerErrors.AlreadyReversed;

        try
        {
            entry.Reverse(request.Reason, _clock.UtcNow);
        }
        catch (DomainException ex) when (ex.Code == "entry.already_reversed")
        {
            return LedgerErrors.AlreadyReversed;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return EntryDto.FromEntity(entry);
    }
}
