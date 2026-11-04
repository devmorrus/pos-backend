using System.Net;
using System.Text.Json;
using MorrusPOS.Application.Common.Exceptions;
using MorrusPOS.Application.Features.Capabilities;

namespace MorrusPOS.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteError(context, HttpStatusCode.Unauthorized, ex.Message);
        }
        catch (CapabilityDisabledException ex)
        {
            await WriteError(context, HttpStatusCode.Forbidden, ex.Message, "CAPABILITY_DISABLED", ex.CapabilityCode, ex.OutletId);
        }
        catch (CapabilityRevisionConflictException ex)
        {
            await WriteError(context, HttpStatusCode.Conflict, ex.Message, "CAPABILITY_REVISION_CONFLICT");
        }
        catch (CapabilityParentDisabledException ex)
        {
            await WriteError(context, HttpStatusCode.Conflict, ex.Message, "CAPABILITY_PARENT_DISABLED");
        }
        catch (KeyNotFoundException ex)
        {
            await WriteError(context, HttpStatusCode.NotFound, ex.Message);
        }
        catch (TrackingValidationException ex)
        {
            await WriteError(context, HttpStatusCode.BadRequest, ex.Message, ex.Code);
        }
        catch (InvalidOperationException ex) // dipakai untuk validasi bisnis, mis. stok tidak cukup
        {
            await WriteError(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteError(context, HttpStatusCode.InternalServerError, "Terjadi kesalahan pada server.");
        }
    }

    private static async Task WriteError(
        HttpContext context,
        HttpStatusCode statusCode,
        string message,
        string? code = null,
        string? capability = null,
        Guid? outletId = null)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        var payload = JsonSerializer.Serialize(new { error = message, code, capability, outletId });
        await context.Response.WriteAsync(payload);
    }
}
