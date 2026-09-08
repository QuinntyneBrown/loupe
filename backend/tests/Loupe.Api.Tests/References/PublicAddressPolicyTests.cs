// Given a resolved address, when classified for outbound fetching,
// then only public unicast addresses pass and every reserved range is rejected.
using System.Net;
using Loupe.Infrastructure.ReferenceImports;
using Xunit;

namespace Loupe.Api.Tests.References;

public sealed class PublicAddressPolicyTests
{
    [Theory]
    [InlineData("8.8.8.8")] // public unicast
    [InlineData("1.1.1.1")] // public unicast
    public void L2_040_1_IPv4_classification(string text) => Assert.True(PublicAddressPolicy.IsPublic(IPAddress.Parse(text)));

    [Theory]
    [InlineData("127.0.0.1")] // loopback
    [InlineData("0.0.0.0")] // this network / unspecified
    [InlineData("10.0.0.1")] // private
    [InlineData("172.16.0.1")] // private
    [InlineData("172.31.255.255")] // private, upper bound
    [InlineData("192.168.1.1")] // private
    [InlineData("169.254.1.1")] // link-local
    [InlineData("169.254.169.254")] // cloud-metadata address
    [InlineData("100.64.0.1")] // carrier-grade NAT
    [InlineData("192.0.0.1")] // IETF protocol assignments
    [InlineData("192.0.2.1")] // TEST-NET-1, reserved
    [InlineData("198.18.0.1")] // benchmarking, reserved
    [InlineData("198.51.100.1")] // TEST-NET-2, reserved
    [InlineData("203.0.113.1")] // TEST-NET-3, reserved
    [InlineData("224.0.0.1")] // multicast
    [InlineData("240.0.0.1")] // reserved
    [InlineData("255.255.255.255")] // broadcast
    public void L2_040_1_IPv4_forbidden_ranges_are_rejected(string text) => Assert.False(PublicAddressPolicy.IsPublic(IPAddress.Parse(text)));

    [Theory]
    [InlineData("2001:4860:4860::8888")] // public unicast (a well-known public resolver)
    public void L2_040_1_IPv6_classification(string text) => Assert.True(PublicAddressPolicy.IsPublic(IPAddress.Parse(text)));

    [Theory]
    [InlineData("::1")] // loopback
    [InlineData("::")] // unspecified
    [InlineData("fe80::1")] // link-local
    [InlineData("fec0::1")] // deprecated site-local
    [InlineData("fc00::1")] // unique local
    [InlineData("fd12:3456:789a::1")] // unique local
    [InlineData("ff02::1")] // multicast
    [InlineData("2001:db8::1")] // documentation, reserved
    public void L2_040_1_IPv6_forbidden_ranges_are_rejected(string text) => Assert.False(PublicAddressPolicy.IsPublic(IPAddress.Parse(text)));

    [Theory]
    [InlineData("::ffff:127.0.0.1")] // IPv4-mapped IPv6 loopback
    [InlineData("::ffff:169.254.169.254")] // IPv4-mapped IPv6 cloud metadata
    [InlineData("::ffff:10.0.0.1")] // IPv4-mapped IPv6 private
    public void L2_040_1_IPv4_mapped_IPv6_forbidden_addresses_are_rejected(string text) => Assert.False(PublicAddressPolicy.IsPublic(IPAddress.Parse(text)));

    [Fact]
    public void L2_040_1_IPv4_mapped_IPv6_public_address_is_accepted() => Assert.True(PublicAddressPolicy.IsPublic(IPAddress.Parse("::ffff:8.8.8.8")));
}
