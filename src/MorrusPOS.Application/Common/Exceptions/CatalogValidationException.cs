namespace MorrusPOS.Application.Common.Exceptions;

public class CatalogValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
