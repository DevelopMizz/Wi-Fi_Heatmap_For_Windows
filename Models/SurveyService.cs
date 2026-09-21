using System.Runtime.InteropServices;
using System.Text;

namespace WiFiServayTool.Models;

public sealed class SurveyService
{
    private readonly List<SignalMeasurement> _measurements = [];

    public IReadOnlyList<SignalMeasurement> Measurements => _measurements;

    public void AddMeasurement(SignalMeasurement measurement)
    {
        _measurements.Add(measurement);
    }

    public double GetAverageSignal()
    {
        if (_measurements.Count == 0)
            return 0;

        return _measurements.Average(x => x.SignalStrengthDbm);
    }

    public SignalMeasurement CaptureMeasurement(double x, double y)
    {
        WifiConnectionInfo wifi = NativeWifi.GetCurrentConnection();

        SignalMeasurement measurement = new()
        {
            Timestamp = DateTime.UtcNow,
            X = x,
            Y = y,
            Ssid = wifi.Ssid,
            Bssid = wifi.Bssid,
            SignalStrengthDbm = wifi.SignalStrengthDbm,
            Channel = wifi.Channel,
            FrequencyMHz = wifi.FrequencyMHz
        };

        AddMeasurement(measurement);

        return measurement;
    }
}

internal sealed class WifiConnectionInfo
{
    public string Ssid { get; init; } = string.Empty;
    public string Bssid { get; init; } = string.Empty;
    public int SignalStrengthDbm { get; init; }
    public int Channel { get; init; }
    public int FrequencyMHz { get; init; }
}

internal static class NativeWifi
{
    private const uint WLAN_CLIENT_VERSION = 2;

    private static readonly string LogFile =
        Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "wifi.log");

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(
                LogFile,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Never allow logging to break measurements
        }
    }

    private enum WLAN_INTERFACE_STATE
    {
        NotReady = 0,
        Connected = 1,
        AdHocNetworkFormed = 2,
        Disconnecting = 3,
        Disconnected = 4,
        Associating = 5,
        Discovering = 6,
        Authenticating = 7
    }

    public static WifiConnectionInfo GetCurrentConnection()
    {
        uint negotiatedVersion;
        IntPtr clientHandle;

        int result = WlanOpenHandle(
            WLAN_CLIENT_VERSION,
            IntPtr.Zero,
            out negotiatedVersion,
            out clientHandle);

        Log($"WlanOpenHandle Result={result}");

        if (result != 0)
        {
            throw new InvalidOperationException(
                $"WlanOpenHandle failed ({result}).");
        }

        try
        {
            IntPtr interfaceListPtr;

            result = WlanEnumInterfaces(
                clientHandle,
                IntPtr.Zero,
                out interfaceListPtr);

            Log($"WlanEnumInterfaces Result={result}");

            if (result != 0)
            {
                throw new InvalidOperationException(
                    $"WlanEnumInterfaces failed ({result}).");
            }

            try
            {
                int itemCount =
                    Marshal.ReadInt32(
                        interfaceListPtr,
                        0);

                Log($"Interface Count={itemCount}");

                if (itemCount == 0)
                {
                    throw new InvalidOperationException(
                        "No Wi-Fi interfaces found.");
                }

                int interfaceInfoOffset = 8;

                int interfaceInfoSize =
                    Marshal.SizeOf<WLAN_INTERFACE_INFO>();

                for (int i = 0; i < itemCount; i++)
                {
                    IntPtr interfacePtr =
                        IntPtr.Add(
                            interfaceListPtr,
                            interfaceInfoOffset +
                            (i * interfaceInfoSize));

                    WLAN_INTERFACE_INFO interfaceInfo =
                        Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(
                            interfacePtr);

                    Log(
                        $"Adapter='{interfaceInfo.InterfaceDescription}' " +
                        $"GUID={interfaceInfo.InterfaceGuid} " +
                        $"State={(WLAN_INTERFACE_STATE)interfaceInfo.IsState}");

                    if ((WLAN_INTERFACE_STATE)interfaceInfo.IsState
                        != WLAN_INTERFACE_STATE.Connected)
                    {
                        Log("Skipping adapter because state is not Connected");
                        continue;
                    }

                    int dataSize;
                    IntPtr dataPtr;
                    WLAN_OPCODE_VALUE_TYPE opcode;

                    result = WlanQueryInterface(
                        clientHandle,
                        ref interfaceInfo.InterfaceGuid,
                        WLAN_INTF_OPCODE
                            .wlan_intf_opcode_current_connection,
                        IntPtr.Zero,
                        out dataSize,
                        out dataPtr,
                        out opcode);

                    Log(
                        $"WlanQueryInterface Result={result} " +
                        $"Adapter='{interfaceInfo.InterfaceDescription}'");

                    if (result != 0)
                    {
                        continue;
                    }

                    try
                    {
                        WLAN_CONNECTION_ATTRIBUTES connection =
                            Marshal.PtrToStructure<WLAN_CONNECTION_ATTRIBUTES>(
                                dataPtr);

                        string ssid =
                            Encoding.ASCII.GetString(
                                connection
                                    .wlanAssociationAttributes
                                    .dot11Ssid
                                    .ucSSID,
                                0,
                                (int)connection
                                    .wlanAssociationAttributes
                                    .dot11Ssid
                                    .uSSIDLength);

                        string bssid =
                            string.Join(
                                ":",
                                connection
                                    .wlanAssociationAttributes
                                    .dot11Bssid
                                    .Select(
                                        x => x.ToString("X2")));

                        uint quality =
                            connection
                                .wlanAssociationAttributes
                                .wlanSignalQuality;

                        int rssi =
                            (int)Math.Round(
                                (quality / 2.0) - 100);

                        uint frequencyKhz =
                            connection
                                .wlanAssociationAttributes
                                .ulChCenterFrequency;

                        int frequencyMHz =
                            (int)(frequencyKhz / 1000);

                        int channel =
                            FrequencyToChannel(
                                frequencyMHz);

                        Log(
                            $"SUCCESS SSID='{ssid}' " +
                            $"BSSID='{bssid}' " +
                            $"RSSI={rssi} " +
                            $"Channel={channel} " +
                            $"FrequencyMHz={frequencyMHz}");

                        return new WifiConnectionInfo
                        {
                            Ssid = ssid,
                            Bssid = bssid,
                            SignalStrengthDbm = rssi,
                            Channel = channel,
                            FrequencyMHz = frequencyMHz
                        };
                    }
                    finally
                    {
                        WlanFreeMemory(dataPtr);
                    }
                }

                Log("No connected Wi-Fi interface found.");

                throw new InvalidOperationException(
                    "No connected Wi-Fi interface found.");
            }
            finally
            {
                WlanFreeMemory(interfaceListPtr);
            }
        }
        catch (Exception ex)
        {
            Log($"EXCEPTION: {ex}");
            throw;
        }
        finally
        {
            WlanCloseHandle(
                clientHandle,
                IntPtr.Zero);
        }
    }

    private static int FrequencyToChannel(
        int frequencyMHz)
    {
        if (frequencyMHz >= 2412 &&
            frequencyMHz <= 2484)
        {
            return ((frequencyMHz - 2412) / 5) + 1;
        }

        if (frequencyMHz >= 5000)
        {
            return (frequencyMHz - 5000) / 5;
        }

        return 0;
    }

    [DllImport("wlanapi.dll")]
    private static extern int WlanOpenHandle(
        uint dwClientVersion,
        IntPtr pReserved,
        out uint pdwNegotiatedVersion,
        out IntPtr phClientHandle);

    [DllImport("wlanapi.dll")]
    private static extern int WlanCloseHandle(
        IntPtr hClientHandle,
        IntPtr pReserved);

    [DllImport("wlanapi.dll")]
    private static extern int WlanEnumInterfaces(
        IntPtr hClientHandle,
        IntPtr pReserved,
        out IntPtr ppInterfaceList);

    [DllImport("wlanapi.dll")]
    private static extern int WlanQueryInterface(
        IntPtr hClientHandle,
        ref Guid pInterfaceGuid,
        WLAN_INTF_OPCODE opCode,
        IntPtr pReserved,
        out int dataSize,
        out IntPtr data,
        out WLAN_OPCODE_VALUE_TYPE valueType);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(
        IntPtr pMemory);

    private enum WLAN_INTF_OPCODE
    {
        wlan_intf_opcode_current_connection = 7
    }

    private enum WLAN_OPCODE_VALUE_TYPE
    {
        QueryOnly = 0
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_INTERFACE_INFO
    {
        public Guid InterfaceGuid;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string InterfaceDescription;

        public int IsState;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DOT11_SSID
    {
        public uint uSSIDLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] ucSSID;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WLAN_ASSOCIATION_ATTRIBUTES
    {
        public DOT11_SSID dot11Ssid;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] dot11Bssid;

        public uint dot11BssType;
        public uint dot11PhyType;
        public uint uDot11PhyIndex;
        public uint wlanSignalQuality;
        public uint ulRxRate;
        public uint ulTxRate;
        public uint ulChCenterFrequency;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_CONNECTION_ATTRIBUTES
    {
        public uint isState;
        public uint wlanConnectionMode;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strProfileName;

        public WLAN_ASSOCIATION_ATTRIBUTES wlanAssociationAttributes;

        public WLAN_SECURITY_ATTRIBUTES wlanSecurityAttributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WLAN_SECURITY_ATTRIBUTES
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool bSecurityEnabled;

        [MarshalAs(UnmanagedType.Bool)]
        public bool bOneXEnabled;

        public uint dot11AuthAlgorithm;
        public uint dot11CipherAlgorithm;
    }
}