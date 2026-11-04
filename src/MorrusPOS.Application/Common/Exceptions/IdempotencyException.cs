namespace MorrusPOS.Application.Common.Exceptions;

public static class IdempotencyErrorCodes
{
    public const string KeyRequired = "IDEMPOTENCY_KEY_REQUIRED";
    public const string KeyInvalid = "IDEMPOTENCY_KEY_INVALID";
    public const string KeyReused = "IDEMPOTENCY_KEY_REUSED";
    public const string InProgress = "IDEMPOTENCY_IN_PROGRESS";
    public const string PaymentReferenceDuplicate = "PAYMENT_REFERENCE_DUPLICATE";
}

public sealed class IdempotencyException : InvalidOperationException
{
    public IdempotencyException(string code, string message) : base(message) => Code = code;
    public string Code { get; }
}
