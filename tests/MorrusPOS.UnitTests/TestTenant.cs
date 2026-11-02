using MorrusPOS.Application.Common.Interfaces;

namespace MorrusPOS.UnitTests;

internal static class TestTenant
{
    public static readonly Guid BusinessId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public static ICurrentUserService CurrentUser { get; } = new TestTenantUser();

    private sealed class TestTenantUser : ICurrentUserService
    {
        public Guid? UserId => Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        public Guid? OutletId => null;
        public Guid? BusinessId => TestTenant.BusinessId;
        public string? Role => "Owner";
        public bool IsAuthenticated => true;
    }
}
