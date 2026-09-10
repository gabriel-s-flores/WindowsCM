// SPDX-License-Identifier: GPL-3.0-or-later
namespace WindowsCM.Core.Settings;

// gschema.xml range parity (research 01 §5). Single source of truth for
// every numeric slider/spin in the settings window so the UI never drifts
// from the ported Copyous limits.
public static class SettingLimits
{
    public const int HistoryLengthMin = 10;
    public const int HistoryLengthMax = 500;
    public const int HistoryLengthDefault = 50;

    public const int HistoryTimeMin = 0;
    public const int HistoryTimeMax = 1440;
    public const int HistoryTimeDefault = 0;

    public const int ClipboardSizeMin = 200;
    public const int ClipboardSizeMax = 10000;
    public const int ClipboardSizeDefault = 500;

    public const int MarginMin = 0;
    public const int MarginMax = 10000;
    public const int MarginDefault = 6;

    public const int ItemWidthMin = 200;
    public const int ItemWidthMax = 1000;
    public const int ItemWidthDefault = 250;

    public const int ItemHeightMin = 50;
    public const int ItemHeightMax = 1000;
    public const int ItemHeightDefault = 170;

    public const int TabWidthMin = 1;
    public const int TabWidthMax = 8;
    public const int TabWidthDefault = 4;

    public const int MaxCharactersMin = 1;
    public const int MaxCharactersMax = 4;
    public const int MaxCharactersDefault = 1;

    // Copyous allows +20dB, but the v1 MediaPlayer backend caps gain at 1.0:
    // +dB would be silently indistinguishable from 0dB, so the max is 0
    // until a DSP mixer restores true boost (SoundOptions parity note).
    public const double VolumeDbMin = -20.0;
    public const double VolumeDbMax = 0.0;
    public const double VolumeDbDefault = 0.0;

    public const int PasteDelayMsMin = 100;
    public const int PasteDelayMsMax = 300;
    public const int PasteDelayMsDefault = 200;

    public static int ClampInt(int value, int min, int max) => Math.Clamp(value, min, max);

    public static double ClampDouble(double value, double min, double max)
    {
        if (double.IsNaN(value))
        {
            return min;
        }
        return Math.Clamp(value, min, max);
    }
}
