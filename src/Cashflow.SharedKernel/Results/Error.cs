namespace Cashflow.SharedKernel.Results;

public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,
    RateLimit = 6,
    DependencyUnavailable = 7,
    Internal = 8
}

public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
    public static Error RateLimit(string code, string message) => new(code, message, ErrorType.RateLimit);
    public static Error DependencyUnavailable(string code, string message) => new(code, message, ErrorType.DependencyUnavailable);
    public static Error Internal(string code, string message) => new(code, message, ErrorType.Internal);
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}
