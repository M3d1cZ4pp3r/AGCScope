using System;
using System.Collections.Generic;
using System.Text;

namespace AGCScope.Services
{
    public static class AgcWordDecoder
    {
        private const ushort WordMask = 0x7FFF;
        private const ushort SignBit = 0x4000;
        private const ushort MagnitudeMask = 0x3FFF;

        private const double WordBase = 16384.0; // 2 ^ 14

        public static double DecodeSP(ushort word)
        {
            word &= WordMask;

            bool negative = (word & SignBit) != 0;

            if (!negative)
                return (word & MagnitudeMask) / WordBase;

            // 1 complement
            ushort magnitude = (ushort)((~word) & MagnitudeMask);

            // Preserve -0
            return magnitude == 0 ? -0.0 : -magnitude / WordBase;
        }

        public static double DecodeDP(ushort high, ushort low)
        {
            return DecodeSP(high) + DecodeSP(low) / WordBase;
        }

        public static double DecodeTP(ushort high, ushort middle, ushort low)
        {
            return DecodeSP(high) + DecodeSP(middle) / WordBase + DecodeSP(low) / (WordBase * WordBase);
        }
    }
}
