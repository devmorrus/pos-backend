namespace MorrusPOS.Application.Common.Interfaces;

/// <summary>
/// Identitas perangkat POS pada request aktif, diisi dari header
/// X-POS-Device-ID/X-POS-Device-Token oleh PosDeviceMiddleware.
/// </summary>
public interface IPosDeviceContext
{
    Guid? DeviceId { get; }
    Guid? RegisterId { get; }
    Guid? OutletId { get; }
    Guid? StockLocationId { get; }
}
