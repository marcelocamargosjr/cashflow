using Cashflow.Ledger.Application.Abstractions.Idempotency;
using Cashflow.Ledger.Application.Entries.Dtos;
using Cashflow.Ledger.Domain.Abstractions;
using Cashflow.Ledger.Domain.Entries;
using Cashflow.SharedKernel.Domain.ValueObjects;
using Cashflow.SharedKernel.Observability;
using Cashflow.SharedKernel.Results;
using Cashflow.SharedKernel.Time;
using MediatR;

namespace Cashflow.Ledger.Application.Entries.Commands.RegisterEntry;

internal sealed class RegisterEntryCommandHandler(
    IEntryRepository entryRepository,
    IUnitOfWork unitOfWork,
    IClock clock)
    : IRequestHandler<RegisterEntryCommand, Result<RegisterEntryResult>>
{
    private readonly IEntryRepository _entryRepository = entryRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IClock _clock = clock;

    public async Task<Result<RegisterEntryResult>> Handle(
        RegisterEntryCommand request,
        CancellationToken cancellationToken)
    {
        var bodyHash = RequestCanonicalizer.Hash(request.ToCanonicalDto());

        var existing = await _entryRepository
            .GetByIdempotencyKeyAsync(request.MerchantId, request.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            if (string.Equals(existing.IdempotencyBodyHash, bodyHash, StringComparison.Ordinal))
            {
                CashflowMeters.IdempotencyHits.Add(
                    1,
                    new KeyValuePair<string, object?>("endpoint", "POST /api/v1/entries"));
                return new RegisterEntryResult(EntryDto.FromEntity(existing), Replayed: true);
            }

            return LedgerErrors.IdempotencyConflict;
        }

        var amount = new Money(request.Amount, ParseCurrency(request.Currency));

        var entry = Entry.Register(
            request.MerchantId,
            request.Type,
            amount,
            request.Description,
            request.Category,
            request.EntryDate,
            request.IdempotencyKey,
            bodyHash,
            _clock.TodayInBusinessZone,
            _clock.UtcNow);

        await _entryRepository.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        CashflowMeters.EntriesRegistered.Add(
            1,
            new KeyValuePair<string, object?>("type", request.Type.ToString()),
            new KeyValuePair<string, object?>("category", request.Category ?? "uncategorized"));

        return new RegisterEntryResult(EntryDto.FromEntity(entry), Replayed: false);
    }

    private static Currency ParseCurrency(string code) =>
        code switch
        {
            "BRL" => Currency.BRL,
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unsupported currency"),
        };
}
