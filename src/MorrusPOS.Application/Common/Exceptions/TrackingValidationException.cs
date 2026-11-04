namespace MorrusPOS.Application.Common.Exceptions;

public static class TrackingErrorCodes
{
    public const string TrackingModeInvalid = "TRACKING_MODE_INVALID";
    public const string BatchRequired = "BATCH_REQUIRED";
    public const string BatchNotFound = "BATCH_NOT_FOUND";
    public const string BatchStockInsufficient = "BATCH_STOCK_INSUFFICIENT";
    public const string SerialRequired = "SERIAL_REQUIRED";
    public const string SerialDuplicate = "SERIAL_DUPLICATE";
    public const string SerialNotAvailable = "SERIAL_NOT_AVAILABLE";
    public const string TrackingAllocationMismatch = "TRACKING_ALLOCATION_MISMATCH";
}

public sealed class TrackingValidationException : InvalidOperationException
{
    public TrackingValidationException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
