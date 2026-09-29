using AGCScope.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace AGCScope.ViewModel
{
    public class SymbolSearchItemViewModel
    {
        public Symbol Symbol { get; }

        public string Name => Symbol.Name;
        public SymbolType Type => Symbol.Type;

        public MemoryType MemoryType => Symbol.Value.MemoryType;

        public string FileName => Symbol.FileName;

        public uint LineNumber => Symbol.LineNumber;

        public string AddressText => FormatAddress(Symbol.Value);

        public SymbolSearchItemViewModel(Symbol symbol)
        {
            Symbol = symbol;
        }

        private static string FormatAddress(SymbolValue value)
        {
            if (value.Kind == ValueKind.Constant)
                return $"={Convert.ToString(value.Value, 8)}";

            if (value.Kind != ValueKind.Address)
                return "-";

            string sreg = Convert.ToString(value.SReg, 8).PadLeft(4, '0');

            if(value.BankingMode.HasFlag(BankingMode.Banked))
            {
                if (!value.Bank.HasValue)
                    return "ERROR";

                string bank = Convert.ToString(value.Bank.Value, 8);

                return value.MemoryType switch
                {
                    MemoryType.Erasable => $"E{bank},{sreg}",
                    MemoryType.Fixed => $"F{bank.PadLeft(2, '0')},{sreg}",
                    _ => sreg
                };
            }

            return sreg;
        }
    }
}
