namespace Cashflow.SharedKernel.Domain.ValueObjects;

// ISO-4217 numeric codes — short keeps storage small and matches the on-disk format.
public enum Currency : short
{
    BRL = 986
    // Future: USD = 840, EUR = 978
}
