using MorrusPOS.Application.Common.Interfaces;

namespace MorrusPOS.Api.Middleware;

/// <summary>
/// Guard sederhana: taruh outlet_id dari JWT claim ke HttpContext.Items supaya
/// semua service/repository di request ini bisa filter data per outlet tanpa
/// perlu terima outlet_id sebagai parameter di setiap method.
///
/// Kalau Role == "Owner" dan outlet_id claim kosong -> akses semua outlet
/// (tidak difilter). Selain itu, wajib match outlet_id di setiap query.
///
/// Untuk endpoint yang mengubah data lintas-outlet secara eksplisit (mis.
/// stock_transfers), validasi tambahan tetap harus ada di service layer.
/// </summary>
public class OutletTenantMiddleware
{
    private readonly RequestDelegate _next;

    public OutletTenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser)
    {
        if (currentUser.IsAuthenticated)
        {
            context.Items["OutletId"] = currentUser.OutletId; // null = Owner
            context.Items["UserId"] = currentUser.UserId;
            context.Items["Role"] = currentUser.Role;
        }

        await _next(context);
    }
}
