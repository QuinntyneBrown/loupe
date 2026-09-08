// Given a source URL and every redirect target, when fetched,
// then only public unicast destinations are ever connected to. See L2-040.
using System.Net;
using Loupe.Application.ReferenceImports;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.References;

public sealed class RestrictedPageFetcherTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private static readonly IPAddress Public = IPAddress.Parse("8.8.8.8");
    private static readonly IPAddress OtherPublic = IPAddress.Parse("8.8.4.4");
    private static readonly IPAddress Loopback = IPAddress.Parse("127.0.0.1");
    private static readonly IPAddress CloudMetadata = IPAddress.Parse("169.254.169.254");
    private static readonly IPAddress MappedLoopback = IPAddress.Parse("::ffff:127.0.0.1");

    private (ApiFactory Factory, ControlledDnsResolver Dns, RecordingSourceConnector Connector) Build()
    {
        var dns = new ControlledDnsResolver();
        var connector = new RecordingSourceConnector();
        var factory = new ApiFactory(database.ConnectionString, database.MediaRoot) { SourceDnsResolver = dns, SourceConnector = connector };
        return (factory, dns, connector);
    }

    private static async Task<IRestrictedPageFetcher> FetcherAsync(ApiFactory factory)
    {
        var scope = factory.Services.CreateAsyncScope();
        return scope.ServiceProvider.GetRequiredService<IRestrictedPageFetcher>();
    }

    [Theory]
    [InlineData("ftp://allowed.example/")] // forbidden scheme
    [InlineData("http://allowed.example:8080/")] // forbidden port
    [InlineData("http://user:pass@allowed.example/")] // embedded credentials
    public async Task L2_040_1_Forbidden_syntax_is_rejected_before_any_dns_or_connect(string url)
    {
        var (factory, dns, connector) = Build();
        await using var _ = factory;
        dns.Map("allowed.example", Public);
        var fetcher = await FetcherAsync(factory);
        var failure = await Assert.ThrowsAsync<SourceFetchException>(() => fetcher.FetchAsync(new Uri(url), default));
        Assert.Equal(SourceFetchFailureKind.ForbiddenDestination, failure.Kind);
        Assert.Empty(connector.Attempts);
    }

    [Fact]
    public async Task L2_040_1_A_literal_or_resolved_internal_address_is_never_connected_to()
    {
        var (factory, dns, connector) = Build();
        await using var _ = factory;
        dns.Map("internal.example", Loopback);
        var fetcher = await FetcherAsync(factory);
        var failure = await Assert.ThrowsAsync<SourceFetchException>(() => fetcher.FetchAsync(new Uri("http://internal.example/"), default));
        Assert.Equal(SourceFetchFailureKind.ForbiddenDestination, failure.Kind);
        Assert.Empty(connector.Attempts);
    }

    [Fact]
    public async Task L2_040_1_Cloud_metadata_and_mapped_forbidden_addresses_are_never_connected_to()
    {
        var (factory, dns, connector) = Build();
        await using var _ = factory;
        dns.Map("metadata.example", CloudMetadata);
        dns.Map("mapped.example", MappedLoopback);
        var fetcher = await FetcherAsync(factory);
        await Assert.ThrowsAsync<SourceFetchException>(() => fetcher.FetchAsync(new Uri("http://metadata.example/"), default));
        await Assert.ThrowsAsync<SourceFetchException>(() => fetcher.FetchAsync(new Uri("http://mapped.example/"), default));
        Assert.Empty(connector.Attempts);
    }

    [Fact]
    public async Task L2_040_1_Given_mixed_dns_answers_only_the_permitted_address_is_connected_to()
    {
        var (factory, dns, connector) = Build();
        await using var _ = factory;
        dns.Map("mixed.example", Loopback, Public); // a forbidden answer first, a permitted one second
        var fetcher = await FetcherAsync(factory);
        using var response = await fetcher.FetchAsync(new Uri("http://mixed.example/"), default);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var attempt = Assert.Single(connector.Attempts);
        Assert.Equal(Public, attempt.Address);
    }

    [Fact]
    public async Task L2_040_2_A_permitted_source_redirecting_to_a_forbidden_destination_is_blocked_before_any_private_bytes_are_retrieved()
    {
        var (factory, dns, connector) = Build();
        await using var _ = factory;
        dns.Map("allowed.example", Public);
        dns.Map("forbidden.example", Loopback);
        connector.EnqueueResponse("HTTP/1.1 302 Found\r\nLocation: http://forbidden.example/\r\nContent-Length: 0\r\n\r\n");
        var fetcher = await FetcherAsync(factory);
        var failure = await Assert.ThrowsAsync<SourceFetchException>(() => fetcher.FetchAsync(new Uri("http://allowed.example/"), default));
        Assert.Equal(SourceFetchFailureKind.ForbiddenDestination, failure.Kind);
        var attempt = Assert.Single(connector.Attempts); // only the first, permitted hop was ever connected to
        Assert.Equal(Public, attempt.Address);
    }

    [Fact]
    public async Task L2_040_2_Redirects_between_two_permitted_destinations_are_followed_and_each_hop_is_revalidated()
    {
        var (factory, dns, connector) = Build();
        await using var _ = factory;
        dns.Map("first.example", Public);
        dns.Map("second.example", OtherPublic);
        connector.EnqueueResponse("HTTP/1.1 302 Found\r\nLocation: http://second.example/\r\nContent-Length: 0\r\n\r\n");
        connector.EnqueueResponse("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
        var fetcher = await FetcherAsync(factory);
        using var response = await fetcher.FetchAsync(new Uri("http://first.example/"), default);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, connector.Attempts.Count);
        Assert.Equal(Public, connector.Attempts[0].Address);
        Assert.Equal(OtherPublic, connector.Attempts[1].Address);
    }

    [Fact]
    public async Task L2_040_2_More_than_five_redirects_stop_without_ever_reaching_a_sixth_destination()
    {
        var (factory, dns, connector) = Build();
        await using var _ = factory;
        for (var hop = 0; hop <= 6; hop++) dns.Map($"hop{hop}.example", Public);
        for (var hop = 0; hop < 6; hop++) connector.EnqueueResponse($"HTTP/1.1 302 Found\r\nLocation: http://hop{hop + 1}.example/\r\nContent-Length: 0\r\n\r\n");
        var fetcher = await FetcherAsync(factory);
        var failure = await Assert.ThrowsAsync<SourceFetchException>(() => fetcher.FetchAsync(new Uri("http://hop0.example/"), default));
        Assert.Equal(SourceFetchFailureKind.RedirectLimitExceeded, failure.Kind);
        Assert.True(connector.Attempts.Count <= 6, "must not exceed the five-redirect budget plus the initial request");
    }

    [Fact]
    public async Task A_fully_permitted_source_with_no_redirect_is_fetched_successfully()
    {
        var (factory, dns, connector) = Build();
        await using var _ = factory;
        dns.Map("plain.example", Public);
        var fetcher = await FetcherAsync(factory);
        using var response = await fetcher.FetchAsync(new Uri("http://plain.example/"), default);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var attempt = Assert.Single(connector.Attempts);
        Assert.Equal(Public, attempt.Address);
        Assert.Equal(80, attempt.Port);
    }
}
