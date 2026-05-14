using Cashflow.Ledger.Application.Entries.Dtos;

namespace Cashflow.Ledger.Application.Entries.Commands.RegisterEntry;

public sealed record RegisterEntryResult(EntryDto Entry, bool Replayed);
