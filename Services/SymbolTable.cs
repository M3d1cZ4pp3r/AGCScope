using System;
using System.Collections.Generic;
using System.Text;

namespace AGCScope.Services
{
    [Flags]
    public enum SymbolType
    {
        None = 0,
        Register = 1,
        Label = 2,
        Variable = 4,
        Constant = 8,
    }

    public enum MemoryType
    {
        None,
        Erasable,
        Fixed
    }

    [Flags]
    public enum BankingMode
    {
        None = 0,
        Unbanked = 1,
        Banked = 2
    }

    public enum ValueKind
    {
        Invalid,
        Constant,
        Address
    }

    public readonly record struct SymbolValue(
        ValueKind Kind,
        ushort SReg,
        MemoryType MemoryType,
        BankingMode BankingMode,
        uint? Bank,
        bool Superbank,
        bool Overflow,
        int Value);

    public sealed record Symbol(
        string Name,
        SymbolValue Value,
        SymbolType Type,
        string FileName,
        uint LineNumber);

    public class SymbolTable
    {
        public string SourcePath { get; init; } = "";
        public List<Symbol> Symbols { get; } = new();
    }
}
