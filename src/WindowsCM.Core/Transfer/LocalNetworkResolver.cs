// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace WindowsCM.Core.Transfer;

// Resolves local IPv4 network interfaces to connect mobile devices on the same LAN/Wi-Fi.
// Prioritizes real physical network interfaces (Wi-Fi or Ethernet with default gateway)
// over virtual adapters (VirtualBox, VMware, VPNs, WSL).
public static class LocalNetworkResolver
{
    private static readonly string[] VirtualKeywords =
    [
        "virtual", "vmware", "vbox", "virtualbox", "hyper-v", "vethernet",
        "loopback", "pseudo", "teredo", "wsl", "tap", "nordvpn", "wireguard", "tailscale"
    ];

    public static IPAddress GetPreferredLocalIp()
    {
        var candidates = GetLocalIpCandidates();
        if (candidates.Count == 0)
        {
            return IPAddress.Loopback;
        }

        // 1. Highest priority: Non-virtual with an active default gateway (standard home/office Wi-Fi or Ethernet)
        var physicalWithGateway = candidates.FirstOrDefault(c => !c.IsVirtual && c.HasGateway);
        if (physicalWithGateway != null)
        {
            return physicalWithGateway.Address;
        }

        // 2. Physical without gateway
        var physical = candidates.FirstOrDefault(c => !c.IsVirtual);
        if (physical != null)
        {
            return physical.Address;
        }

        // 3. Fallback to any non-loopback candidate
        return candidates[0].Address;
    }

    public static IReadOnlyList<IPAddress> GetAllLocalIps()
    {
        return GetLocalIpCandidates().Select(c => c.Address).ToList();
    }

    private sealed record IpCandidate(
        IPAddress Address,
        bool HasGateway,
        bool IsVirtual,
        NetworkInterfaceType InterfaceType);

    private static List<IpCandidate> GetLocalIpCandidates()
    {
        var result = new List<IpCandidate>();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var iface in interfaces)
            {
                if (iface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }
                if (iface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var isVirtual = IsVirtualInterface(iface.Name, iface.Description);
                var ipProps = iface.GetIPProperties();
                var hasGateway = ipProps.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork
                    && !g.Address.Equals(IPAddress.Any)
                    && !g.Address.ToString().StartsWith("0.0.0.0"));

                foreach (var unicast in ipProps.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    var ipStr = unicast.Address.ToString();
                    if (ipStr.StartsWith("127.") || ipStr.StartsWith("169.254."))
                    {
                        continue;
                    }

                    result.Add(new IpCandidate(
                        unicast.Address,
                        hasGateway,
                        isVirtual,
                        iface.NetworkInterfaceType));
                }
            }
        }
        catch
        {
            // Network interface querying should never crash the app
        }

        return result
            .OrderByDescending(c => !c.IsVirtual && c.HasGateway)
            .ThenByDescending(c => !c.IsVirtual)
            .ThenByDescending(c => c.InterfaceType == NetworkInterfaceType.Wireless80211)
            .ThenByDescending(c => c.InterfaceType == NetworkInterfaceType.Ethernet)
            .ToList();
    }

    private static bool IsVirtualInterface(string name, string description)
    {
        var lower = (name + " " + description).ToLowerInvariant();
        return VirtualKeywords.Any(k => lower.Contains(k));
    }
}
