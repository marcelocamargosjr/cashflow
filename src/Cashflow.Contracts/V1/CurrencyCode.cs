namespace Cashflow.Contracts.V1;

/// <summary>
/// Currency code helpers for the wire format.
///
/// On the wire, Currency travels as ISO-4217 alpha-3 (e.g. "BRL"). Internally,
/// Ledger stores it as `enum Currency : short` (ISO-4217 numeric, e.g. 986).
/// This helper is the SINGLE point of conversion both directions — never call
/// `Enum.Parse` or `.ToString()` on a `Currency` outside this class. Keeps
/// consumers (potentially non-.NET) decoupled from the BCL enum name format.
/// </summary>
public static class CurrencyCode
{
    public static short ToNumeric(string alpha3) => alpha3 switch
    {
        "BRL" => 986,
        _ => throw new ArgumentException($"Unsupported currency code: '{alpha3}'", nameof(alpha3))
    };

    public static string ToAlpha3(short numeric) => numeric switch
    {
        986 => "BRL",
        _ => throw new ArgumentException($"Unsupported currency numeric: {numeric}", nameof(numeric))
    };
}
