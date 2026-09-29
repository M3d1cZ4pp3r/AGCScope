using AGCScope.ViewModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AGCScope.Services
{
    public static class AgcWordFormatter
    {
        public static string FormatValue(ReadOnlySpan<ushort> words, ValueFormat format)
        {
            if (words.Length == 0)
                return "-";

            return format switch
            {
                ValueFormat.Octal => FormatOctal(words),
                ValueFormat.Decimal => FormatDecimal(words),
                ValueFormat.SP => FormatSP(words),
                ValueFormat.DP => FormatDP(words),
                ValueFormat.TP => FormatTP(words),

                _ => "-"
            };

        }

        private static string FormatOctal(ReadOnlySpan<ushort> words)
        {
            return string.Join(" ", words.ToArray().Select(x => Convert.ToString(x & 0x7FFF, 8).PadLeft(5, '0')));
        }

        private static string FormatDecimal(ReadOnlySpan<ushort> words)
        {
            return string.Join(" ", words.ToArray().Select(x => Convert.ToString(x & 0x7FFF, 10)));
        }

        private static string FormatSP(ReadOnlySpan<ushort> words)
        {
            return string.Join("  ", words.ToArray().Select(x => AgcWordDecoder.DecodeSP(x).ToString("G10", CultureInfo.InvariantCulture)));
        }

        private static string FormatDP(ReadOnlySpan<ushort> words)
        {
            if (words.Length % 2 != 0)
                return "<invalid DP length>";

            var values = new List<string>();

            for (int i = 0; i < words.Length; i += 2)
            {
                double value = AgcWordDecoder.DecodeDP(words[i], words[i + 1]);
                values.Add(value.ToString("G10"));
            }

            return string.Join("  ", values);
        }

        private static string FormatTP(ReadOnlySpan<ushort> words)
        {
            if (words.Length % 3 != 0)
                return "<invalid TP length>";

            var values = new List<string>();

            for (int i = 0; i < words.Length; i += 3)
            {
                double value = AgcWordDecoder.DecodeTP(words[i], words[i + 1], words[i + 2]);
                values.Add(value.ToString("G10"));
            }

            return string.Join("  ", values);
        }
    }
}
