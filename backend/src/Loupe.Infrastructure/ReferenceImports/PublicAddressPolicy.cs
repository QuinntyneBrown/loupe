using System.Net;
using System.Net.Sockets;

namespace Loupe.Infrastructure.ReferenceImports;

/// <summary>
/// Classifies a resolved address as a public unicast destination. Private, loopback, link-local,
/// reserved, multicast, unspecified, and cloud-metadata addresses are rejected for both IPv4 and
/// IPv6, including IPv4-mapped IPv6 forms. See L2-040.
/// </summary>
public static class PublicAddressPolicy
{
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicIPv4(address),
            AddressFamily.InterNetworkV6 => IsPublicIPv6(address),
            _ => false
        };
    }

    private static bool IsPublicIPv4(IPAddress address)
    {
        var b = address.GetAddressBytes();
        if (IPAddress.IsLoopback(address)) return false; // 127.0.0.0/8
        if (b[0] == 0) return false; // 0.0.0.0/8 "this network"
        if (b[0] == 10) return false; // 10.0.0.0/8 private
        if (b[0] == 172 && b[1] is >= 16 and <= 31) return false; // 172.16.0.0/12 private
        if (b[0] == 192 && b[1] == 168) return false; // 192.168.0.0/16 private
        if (b[0] == 169 && b[1] == 254) return false; // 169.254.0.0/16 link-local, incl. cloud metadata 169.254.169.254
        if (b[0] == 100 && b[1] is >= 64 and <= 127) return false; // 100.64.0.0/10 carrier-grade NAT
        if (b[0] == 192 && b[1] == 0 && b[2] == 0) return false; // 192.0.0.0/24 IETF protocol assignments
        if (b[0] == 192 && b[1] == 0 && b[2] == 2) return false; // 192.0.2.0/24 TEST-NET-1 (reserved)
        if (b[0] == 198 && b[1] is 18 or 19) return false; // 198.18.0.0/15 benchmarking (reserved)
        if (b[0] == 198 && b[1] == 51 && b[2] == 100) return false; // 198.51.100.0/24 TEST-NET-2 (reserved)
        if (b[0] == 203 && b[1] == 0 && b[2] == 113) return false; // 203.0.113.0/24 TEST-NET-3 (reserved)
        if (b[0] >= 224) return false; // 224.0.0.0/4 multicast, 240.0.0.0/4 reserved, 255.255.255.255 broadcast
        return true;
    }

    private static bool IsPublicIPv6(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return false; // ::1
        if (address.Equals(IPAddress.IPv6Any)) return false; // :: unspecified
        if (address.IsIPv6LinkLocal) return false; // fe80::/10
        if (address.IsIPv6SiteLocal) return false; // fec0::/10 deprecated site-local
        if (address.IsIPv6Multicast) return false; // ff00::/8
        var b = address.GetAddressBytes();
        if ((b[0] & 0xFE) == 0xFC) return false; // fc00::/7 unique local
        if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8) return false; // 2001:db8::/32 documentation (reserved)
        return true;
    }
}
