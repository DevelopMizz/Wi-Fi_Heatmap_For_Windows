namespace WiFiServayTool.Models;

public sealed class SignalMeasurement
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    // Survey position on the floor plan
    public double X { get; init; }
    public double Y { get; init; }

    // Access Point information
    public string Ssid { get; init; } = string.Empty;
    public string Bssid { get; init; } = string.Empty;

    // Signal strength in dBm
    public int SignalStrengthDbm { get; init; }

    // Channel information
    public int Channel { get; init; }

    // Frequency in MHz
    public int FrequencyMHz { get; init; }

    public override string ToString()
    {
        return $"{Ssid} ({SignalStrengthDbm} dBm) @ [{X}, {Y}]";
    }
}