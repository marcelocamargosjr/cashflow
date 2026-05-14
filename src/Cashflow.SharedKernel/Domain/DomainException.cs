namespace Cashflow.SharedKernel.Domain;

// Messages here are stable domain error codes (English), NOT user-facing strings.
// PT-BR translation happens at the edge (ProblemDetails middleware) by mapping
// an error.code to the localized message. This keeps the domain pure and
// testable in any language, and separates i18n concerns from invariants.
public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string message)
        : base(message)
    {
        Code = message;
    }

    public DomainException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public DomainException(string code, string message, Exception inner)
        : base(message, inner)
    {
        Code = code;
    }

    public DomainException() : base()
    {
    }

    public DomainException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
