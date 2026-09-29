using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace AGCScope.Services
{
    // Managed languages are funny if you want to interpret file bytes with a struct

    [InlineArray(11)]
    struct Byte11
    {
        private byte element;
    }

    [InlineArray(257)]
    public struct Byte257
    {
        private byte element;
    }

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Ansi, Size = 1032)]
    struct SymtabHeader
    {
        // Works because of offset 0
        [FieldOffset(0)]
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
        public string SourcePath;

        [FieldOffset(1024)]
        public int NumberSymbols;

        [FieldOffset(1028)]
        public int NumberLines;
    }

    [StructLayout(LayoutKind.Explicit, Size = 292)]
    struct SymtabSymbol
    {
        [FieldOffset(0)]
        public byte Namespace;

        [FieldOffset(1)]
        public Byte11 Name;

        [FieldOffset(12)]
        public SymtabAddress Value;

        [FieldOffset(24)]
        public int Type;

        [FieldOffset(28)]
        public Byte257 FileName;

        // 3 bytes padding, probably

        [FieldOffset(288)]
        public uint LineNumber;
    }

    [StructLayout(LayoutKind.Explicit, Size = 12)]
    struct SymtabAddress
    {
        // Pray that the padding is predictible in the symtab file
        [FieldOffset(0)]
        public uint Flags;

        [FieldOffset(4)]
        public int Value;

        [FieldOffset(8)]
        public int Syllable;

        public bool Invalid => (Flags & (1u << 0)) != 0;
        public bool Constant => (Flags & (1u << 1)) != 0;
        public bool IsAddress => (Flags & (1u << 2)) != 0;

        public ushort SReg => (ushort)((Flags >> 3) & 0xFFF);

        public bool Erasable => (Flags & (1u << 15)) != 0;
        public bool Fixed => (Flags & (1u << 16)) != 0;
        public bool Unbanked => (Flags & (1u << 17)) != 0;
        public bool Banked => (Flags & (1u << 18)) != 0;

        public uint EB => (Flags >> 19) & 0x7;
        public uint FB => (Flags >> 22) & 0x1F;

        public bool Super => (Flags & (1u << 27)) != 0;
        public bool Overflow => (Flags & (1u << 28)) != 0;
    }

    internal class SymtabLoader
    {
        public static SymbolTable Load(string path)
        {
            using var stream = File.OpenRead(path);

            SymtabHeader header = ReadStruct<SymtabHeader>(stream);

            if(new FileInfo(path).Length < (Marshal.SizeOf<SymtabHeader>() + header.NumberSymbols * Marshal.SizeOf<SymtabSymbol>()))
            {
                throw new InvalidDataException("Symtab doesn't contain all symbols it describes");
            }

            SymbolTable table = new() { SourcePath = header.SourcePath };

            for(int i = 0; i < header.NumberSymbols; i++)
            {
                SymtabSymbol symbol = ReadStruct<SymtabSymbol>(stream);

                ValueKind kind = symbol.Value.Invalid ? ValueKind.Invalid : (symbol.Value.Constant ? ValueKind.Constant : ValueKind.Address);
                MemoryType type = symbol.Value.Fixed ? MemoryType.Fixed : (symbol.Value.Erasable ? MemoryType.Erasable : MemoryType.None);
                BankingMode mode = (symbol.Value.Unbanked ? BankingMode.Unbanked : BankingMode.None) | (symbol.Value.Banked ? BankingMode.Banked : BankingMode.None);
                uint? bank = type == MemoryType.Fixed ? symbol.Value.FB : (type == MemoryType.Erasable ? symbol.Value.EB : null);

                string name = ReadCString(symbol.Name);
                string fileName = ReadCString(symbol.FileName);
                

                // Convert to internal data model
                table.Symbols.Add(
                    new(
                        name,
                        new(kind, symbol.Value.SReg, type, mode, bank, symbol.Value.Super, symbol.Value.Overflow, symbol.Value.Value),
                        (SymbolType)symbol.Type,
                        fileName,
                        symbol.LineNumber));
            }

            return table;
        }

        private static string ReadCString(ReadOnlySpan<byte> bytes)
        {
            int length = bytes.IndexOf((byte)0);

            if (length < 0)
                length = bytes.Length;

            return Encoding.ASCII.GetString(bytes[..length]);
        }

        private static T ReadStruct<T>(Stream stream) where T : struct
        {
            int size = Marshal.SizeOf<T>();

            byte[] buffer = new byte[size];
            stream.ReadExactly(buffer);

            // Prevent buffer from being relocated while passing around a pointer
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            try
            {
                IntPtr ptr = handle.AddrOfPinnedObject();
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
