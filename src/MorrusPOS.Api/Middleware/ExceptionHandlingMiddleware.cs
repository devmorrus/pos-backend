using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using MorrusPOS.Application.Common.Exceptions;
using MorrusPOS.Application.Features.Capabilities;
using MorrusPOS.Application.Common.Observability;

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
        catch (StockIntegrityException ex)
        {
            var status = ex.Code is StockIntegrityErrorCodes.PostingConflict or
                StockIntegrityErrorCodes.ReconciliationBlocked or StockIntegrityErrorCodes.LedgerImmutable or
                StockIntegrityErrorCodes.TransferStatusInvalid or StockIntegrityErrorCodes.TransferAlreadyReceived
                ? HttpStatusCode.Conflict
                : HttpStatusCode.BadRequest;
            await WriteError(context, status, ex.Message, ex.Code);
        }
        catch (IdempotencyException ex)
        {
            var status = ex.Code is IdempotencyErrorCodes.KeyReused or
                IdempotencyErrorCodes.InProgress or IdempotencyErrorCodes.PaymentReferenceDuplicate
                ? HttpStatusCode.Conflict
                : HttpStatusCode.BadRequest;
            if (ex.Code == IdempotencyErrorCodes.InProgress)
                context.Response.Headers.RetryAfter = "1";
            await WriteError(context, status, ex.Message, ex.Code);
        }
        catch (AccountingIntegrityException ex)
        {
            var status = ex.Code is AccountingIntegrityErrorCodes.JournalPostingConflict or
                AccountingIntegrityErrorCodes.JournalImmutable or
                AccountingIntegrityErrorCodes.JournalReferenceDuplicate or
                AccountingIntegrityErrorCodes.JournalReversalInvalid or
                AccountingIntegrityErrorCodes.AccountMappingMissing or
                AccountingIntegrityErrorCodes.AccountingNotReady or
                AccountingIntegrityErrorCodes.AccountingReconciliationBlocked
                ? HttpStatusCode.Conflict
                : HttpStatusCode.BadRequest;
            await WriteError(context, status, ex.Message, ex.Code);
        }
        catch (PostgresException ex) when (ex.MessageText.StartsWith("JOURNAL_", StringComparison.Ordinal))
        {
            await WriteJournalError(context, ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException journalPg &&
            journalPg.MessageText.StartsWith("JOURNAL_", StringComparison.Ordinal))
        {
            await WriteJournalError(context, journalPg);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException paymentPg &&
            paymentPg.ConstraintName is "UX_payments_Business_Method_Reference" or
                "UX_supplier_payments_Business_Method_Reference")
        {
            await WriteError(context, HttpStatusCode.Conflict,
                "Nomor referensi pembayaran sudah digunakan.",
                IdempotencyErrorCodes.PaymentReferenceDuplicate);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException postgres && IsStockIntegrityError(postgres))
        {
            var pg = (PostgresException)ex.InnerException!;
            var postingConflict = pg.ConstraintName == "IX_stock_ledger_BusinessId_PostingKey";
            await WriteError(context,
                postingConflict ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest,
                postingConflict ? "Pergerakan stok ini sudah pernah diposting." : "Pergerakan stok ditolak karena akan membuat saldo atau ledger tidak valid.",
                postingConflict ? StockIntegrityErrorCodes.PostingConflict : StockIntegrityErrorCodes.MovementInvalid);
        }
        catch (InvalidOperationException ex) // dipakai untuk validasi bisnis, mis. stok tidak cukup
        {
            await WriteError(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (CatalogValidationException ex)
        {
            await WriteError(context, ex.Code.EndsWith("_DUPLICATE", StringComparison.Ordinal) ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest, ex.Message, ex.Code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteError(context, HttpStatusCode.InternalServerError, "Terjadi kesalahan pada server.");
        }
    }

    private static Task WriteJournalError(HttpContext context, PostgresException ex)
    {
        var code = ex.MessageText.Split(':', 2)[0];
        var status = code is AccountingIntegrityErrorCodes.JournalPostingConflict or
            AccountingIntegrityErrorCodes.JournalImmutable or
            AccountingIntegrityErrorCodes.JournalReferenceDuplicate or
            AccountingIntegrityErrorCodes.JournalReversalInvalid
            ? HttpStatusCode.Conflict
            : HttpStatusCode.BadRequest;
        return WriteError(context, status, ex.MessageText, code);
    }

    private static bool IsStockIntegrityError(PostgresException ex)
        => ex.ConstraintName?.StartsWith("CK_stock_", StringComparison.Ordinal) == true ||
           ex.ConstraintName == "IX_stock_ledger_BusinessId_PostingKey" ||
           ex.MessageText.StartsWith("STOCK_", StringComparison.Ordinal);

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
        MorrusTelemetry.RecordException(code ?? $"HTTP_{(int)statusCode}", (int)statusCode);
        var payload = JsonSerializer.Serialize(new { error = message, code, capability, outletId });
        await context.Response.WriteAsync(payload);
    }
}
