using MorrusPOS.Api.Security;

namespace MorrusPOS.IntegrationTests;

public class PosCredentialsTests
{
    [Fact]
    public void PairingCodeAndDeviceToken_AreOneWayAndVerifiable()
    {
        var code = PosCredentials.CreatePairingCode();
        var token = PosCredentials.CreateDeviceToken();

        Assert.Equal(8, code.Length);
        Assert.True(PosCredentials.Matches(
            PosCredentials.HashPairingCode(code),
            PosCredentials.HashPairingCode(code.ToLowerInvariant())));
        Assert.True(PosCredentials.Matches(
            PosCredentials.HashDeviceToken(token),
            PosCredentials.HashDeviceToken(token)));
        Assert.False(PosCredentials.Matches(
            PosCredentials.HashDeviceToken(token),
            PosCredentials.HashDeviceToken(token + "changed")));
    }
}
