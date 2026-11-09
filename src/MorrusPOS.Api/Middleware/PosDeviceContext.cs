using MorrusPOS.Application.Common.Interfaces;

namespace MorrusPOS.Api.Middleware;

public sealed class PosDeviceContext : IPosDeviceContext
{
    internal const string DeviceIdKey = "PosDeviceId";
    internal const string RegisterIdKey = "PosRegisterId";
    internal const string OutletIdKey = "PosOutletId";
    internal const string StockLocationIdKey = "PosStockLocationId";

    private readonly IHttpContextAccessor _accessor;

    public PosDeviceContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Guid? DeviceId => Get(DeviceIdKey);
    public Guid? RegisterId => Get(RegisterIdKey);
    public Guid? OutletId => Get(OutletIdKey);
    public Guid? StockLocationId => Get(StockLocationIdKey);

    private Guid? Get(string key)
        => _accessor.HttpContext?.Items.TryGetValue(key, out var value) == true && value is Guid id
            ? id
            : null;
}
