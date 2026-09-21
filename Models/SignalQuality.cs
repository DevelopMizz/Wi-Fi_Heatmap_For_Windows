using System.Drawing;

namespace WiFiServayTool.Models;

public static class SignalQuality
{
    public static Color GetSignalColour(int rssi)
    {
        if (rssi >= -55)
            return Color.Green;

        if (rssi >= -67)
            return Color.Yellow;

        if (rssi >= -75)
            return Color.Orange;

        return Color.Red;
    }
}