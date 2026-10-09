using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace WorldHub.Network.Services;

public sealed class RadminVpnDetector
{
    public RadminVpnAdapterInfo? FindAdapter()
    {
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            var adapterIdentity = $"{adapter.Name} {adapter.Description}";

            if (!adapterIdentity.Contains(
                    "Radmin",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var address = adapter.GetIPProperties()
                .UnicastAddresses
                .Select(item => item.Address)
                .FirstOrDefault(IsValidRadminAddress);

            if (address is null)
            {
                continue;
            }

            return new RadminVpnAdapterInfo(
                adapter.Name,
                adapter.Description,
                address);
        }

        return null;
    }

    /// <summary>
    /// Валидный IP Radmin: IPv4, не loopback, не 0.0.0.0,
    /// не APIPA (169.254.x.x — значит, адаптер не получил адрес).
    /// </summary>
    private static bool IsValidRadminAddress(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        if (IPAddress.IsLoopback(ip))
        {
            return false;
        }

        if (ip.Equals(IPAddress.Any))
        {
            return false;
        }

        var bytes = ip.GetAddressBytes();

        // APIPA: 169.254.x.x — адаптер не получил адрес от DHCP.
        if (bytes[0] == 169 && bytes[1] == 254)
        {
            return false;
        }

        return true;
    }
}

public sealed record RadminVpnAdapterInfo(
    string Name,
    string Description,
    IPAddress Address);