using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace AGCScope.Services
{
    
    [InlineArray(36 * 1024)]
    internal struct MemoryBuffer
    {
        private ushort element0;
    }

    public class RopeImage
    {
        public const int Banks = 36;
        public const int WordsPerBank = 1024;

        private MemoryBuffer buffer;

        public ushort this[int bank, int word]
        {
            get
            {
                Validate(bank, word);
                return buffer[bank * WordsPerBank + word];
            }
            set
            {
                Validate(bank, word);
                buffer[bank * WordsPerBank + word] = value;
            }
        }

        public Span<ushort> GetBlock(int startIndex, int length)
        {
            return MemoryMarshal.CreateSpan(ref buffer[startIndex], length);
        }

        public Span<byte> GetAsBytes()
        {
            return MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref buffer[0], Banks * WordsPerBank));
        }

        private static void Validate(int bank, int word)
        {
            if(bank >= Banks || word >= WordsPerBank)
            {
                throw new ArgumentOutOfRangeException();
            }
        }
    }
}
