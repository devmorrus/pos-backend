using System.Collections.Specialized;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MorrusPOS.Application.Features.Channels;
using MorrusPOS.Infrastructure.Options;
using MorrusPOS.Infrastructure.Services;
using Xunit;

namespace MorrusPOS.UnitTests;

public class GoBizOAuthTests
{
    [Fact]
    public async Task CreateAuthorizationUrlAsync_Should_Contain_RequiredParameters_And_ExactRedirectUri()
    {
        var options = Options.Create(CreateValidOptions());
        var stateStore = new GoBizOAuthStateStore(new MemoryCache(new MemoryCacheOptions()));
        var service = new GoBizAuthService(options, stateStore);

        var result = await service.CreateAuthorizationUrlAsync(Guid.NewGuid(), Guid.NewGuid());
        var uri = new Uri(result.AuthorizationUrl);
        var query = ParseQuery(uri.Query);

        query["response_type"].Should().Be("code");
        query["scope"].Should().Be("openid");
        query["user_type"].Should().Be("merchant");
        query["client_id"].Should().Be(options.Value.ClientId);
        query["state"].Should().NotBeNullOrWhiteSpace();
        query["redirect_uri"].Should().Be(options.Value.RedirectUri);
        result.State.Should().Be(query["state"]);
    }

    [Fact]
    public async Task CreateAuthorizationUrlAsync_Twice_Should_Generate_DifferentStates()
    {
        var options = Options.Create(CreateValidOptions());
        var stateStore = new GoBizOAuthStateStore(new MemoryCache(new MemoryCacheOptions()));
        var service = new GoBizAuthService(options, stateStore);

        var first = await service.CreateAuthorizationUrlAsync(Guid.NewGuid(), Guid.NewGuid());
        var second = await service.CreateAuthorizationUrlAsync(Guid.NewGuid(), Guid.NewGuid());

        first.State.Should().NotBe(second.State);
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WithUnknownState_Should_ReturnNull()
    {
        var store = new GoBizOAuthStateStore(new MemoryCache(new MemoryCacheOptions()));

        var result = await store.ValidateAndConsumeAsync("unknown-state");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_Should_OnlyAllowOneTimeUse()
    {
        var store = new GoBizOAuthStateStore(new MemoryCache(new MemoryCacheOptions()));
        var created = await store.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromMinutes(10));

        var first = await store.ValidateAndConsumeAsync(created.State);
        var second = await store.ValidateAndConsumeAsync(created.State);

        first.Should().NotBeNull();
        second.Should().BeNull();
    }

    [Fact]
    public void GoBizOptionsValidator_Should_Fail_WhenRedirectUriInvalid()
    {
        var validator = new GoBizOptionsValidator(new FakeHostEnvironment("Production"));
        var options = CreateValidOptions();
        options.RedirectUri = "http://example.com/callback?x=1";

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void GoBizOptionsValidator_Should_Pass_ForHttpsLocalhostInDevelopment()
    {
        var validator = new GoBizOptionsValidator(new FakeHostEnvironment("Development"));
        var result = validator.Validate(null, CreateValidOptions());

        result.Succeeded.Should().BeTrue();
    }

    private static GoBizOptions CreateValidOptions() => new()
    {
        Environment = "Sandbox",
        ClientId = "client-id",
        ClientSecret = "client-secret",
        PartnerId = "partner-id",
        RedirectUri = "https://localhost:7100/api/gobiz/callback",
        AuthorizationUrl = "https://integration-goauth.gojekapi.com/oauth2/auth",
        TokenUrl = "https://integration-goauth.gojekapi.com/oauth2/token",
        ApiBaseUrl = "https://api.partner-sandbox.gobiz.co.id",
        Scope = "openid",
        UserType = "merchant",
        Prompt = "login"
    };

    private static NameValueCollection ParseQuery(string queryString)
    {
        var result = new NameValueCollection();
        var query = queryString.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var segments = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(segments[0]);
            var value = segments.Length > 1 ? Uri.UnescapeDataString(segments[1]) : string.Empty;
            result[key] = value;
        }

        return result;
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = default!;
    }
}
