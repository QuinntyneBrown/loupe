// Given an origin's robots.txt, when evaluated for the declared Loupe importer agent,
// then a missing file permits fetching, a matching disallow blocks it, and an
// authentication error, server error, or oversized response defers rather than guesses.
using Loupe.Application.ReferenceImports;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.References;

public sealed class RobotsPolicyTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private static readonly System.Net.IPAddress Public = System.Net.IPAddress.Parse("8.8.8.8");

    private (ApiFactory Factory, ControlledDnsResolver Dns, RecordingSourceConnector Connector) Build()
    {
        var dns = new ControlledDnsResolver();
        var connector = new RecordingSourceConnector();
        var factory = new ApiFactory(database.ConnectionString, database.MediaRoot) { SourceDnsResolver = dns, SourceConnector = connector };
        return (factory, dns, connector);
    }

    private static async Task<IRobotsPolicy> PolicyAsync(ApiFactory factory) =>
        factory.Services.CreateAsyncScope().ServiceProvider.GetRequiredService<IRobotsPolicy>();

    private static string Response(int status, string reason, string body = "") =>
        $"HTTP/1.1 {status} {reason}\r\nContent-Length: {System.Text.Encoding.UTF8.GetByteCount(body)}\r\n\r\n{body}";

    [Fact]
    public async Task L2_011_1_A_missing_robots_file_permits_fetching()
    {
        var (factory, dns, connector) = Build();
        await using var _ = factory;
        dns.Map("no-robots.example", Public);
        connector.EnqueueResponse(Response(404, "Not Found"));
        var policy = await PolicyAsync(factory);
        Assert.Equal(RobotsDecision.Allowed, await policy.EvaluateAsync(new Uri("http://no-robots.example/photo.jpg"), default));
    }

    [Fact]
    public async Task L2_011_1_A_matching_disallow_for_the_wildcard_agent_blocks_the_path()
    {
        var (factory, dns, connector) = Build();
        await using var _ = factory;
        dns.Map("blocked.example", Public);
        connector.EnqueueResponse(Response(200, "OK", "User-agent: *\nDisallow: /private\n"));
        var policy = await PolicyAsync(factory);
        Assert.Equal(RobotsDecision.Disallowed, await policy.EvaluateAsync(new Uri("http://blocked.example/private/photo.jpg"), default));
    }

    [Fact]
    public async Task L2_011_1_A_disallow_that_does_not_match_the_path_permits_fetching()
    {
        var (factory, dns, connector) = Build();
        await using var _ = factory;
        dns.Map("partial.example", Public);
        connector.EnqueueResponse(Response(200, "OK", "User-agent: *\nDisallow: /private\n"));
        var policy = await PolicyAsync(factory);
        Assert.Equal(RobotsDecision.Allowed, await policy.EvaluateAsync(new Uri("http://partial.example/public/photo.jpg"), default));
    }

    [Fact]
    public async Task L2_011_1_A_Loupe_specific_group_takes_precedence_over_the_wildcard_group()
    {
        var (factory, dns, connector) = Build();
        await using var _ = factory;
        dns.Map("specific.example", Public);
        connector.EnqueueResponse(Response(200, "OK", "User-agent: Loupe\nDisallow: /\n\nUser-agent: *\nAllow: /\n"));
        var policy = await PolicyAsync(factory);
        Assert.Equal(RobotsDecision.Disallowed, await policy.EvaluateAsync(new Uri("http://specific.example/anything.jpg"), default));
    }

    [Fact]
    public async Task L2_011_1_The_longer_matching_rule_wins_within_one_group()
    {
        var (factory, dns, connector) = Build();
        await using var _ = factory;
        dns.Map("mixed.example", Public);
        connector.EnqueueResponse(Response(200, "OK", "User-agent: *\nDisallow: /\nAllow: /public/\n"));
        var policy = await PolicyAsync(factory);
        Assert.Equal(RobotsDecision.Allowed, await policy.EvaluateAsync(new Uri("http://mixed.example/public/photo.jpg"), default));
        connector.EnqueueResponse(Response(200, "OK", "User-agent: *\nDisallow: /\nAllow: /public/\n"));
        Assert.Equal(RobotsDecision.Disallowed, await policy.EvaluateAsync(new Uri("http://mixed.example/private/photo.jpg"), default));
    }

    [Theory]
    [InlineData(401, "Unauthorized")]
    [InlineData(403, "Forbidden")]
    [InlineData(500, "Internal Server Error")]
    [InlineData(503, "Service Unavailable")]
    public async Task L2_011_1_An_authentication_or_server_error_defers_rather_than_guesses(int status, string reason)
    {
        var (factory, dns, connector) = Build();
        await using var _ = factory;
        dns.Map("erroring.example", Public);
        connector.EnqueueResponse(Response(status, reason));
        var policy = await PolicyAsync(factory);
        Assert.Equal(RobotsDecision.Unavailable, await policy.EvaluateAsync(new Uri("http://erroring.example/photo.jpg"), default));
    }

    [Fact]
    public async Task L2_040_An_oversized_robots_response_defers_rather_than_truncates_and_guesses()
    {
        var (factory, dns, connector) = Build();
        await using var _ = factory;
        dns.Map("huge.example", Public);
        var body = "User-agent: *\n" + new string('#', 512_001) + "\nDisallow: /\n";
        connector.EnqueueResponse(Response(200, "OK", body));
        var policy = await PolicyAsync(factory);
        Assert.Equal(RobotsDecision.Unavailable, await policy.EvaluateAsync(new Uri("http://huge.example/photo.jpg"), default));
    }

    [Fact]
    public async Task An_unreachable_origin_defers_rather_than_guesses()
    {
        var (factory, dns, connector) = Build();
        await using var _ = factory;
        dns.Map("unresolvable.example"); // no addresses at all
        var policy = await PolicyAsync(factory);
        Assert.Equal(RobotsDecision.Unavailable, await policy.EvaluateAsync(new Uri("http://unresolvable.example/photo.jpg"), default));
        Assert.Empty(connector.Attempts);
    }
}
