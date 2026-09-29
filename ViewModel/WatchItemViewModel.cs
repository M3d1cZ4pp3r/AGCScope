using AGCScope.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace AGCScope.ViewModel
{
    public enum ValueFormat
    {
        Octal,
        Decimal,
        SP,
        DP,
        TP
    }

    public partial class WatchItemViewModel : ObservableObject
    {
        public Symbol Symbol { get; }

        public string Name => Symbol.Name;
        public SymbolType Type => Symbol.Type;

        public MemoryType MemoryType => Symbol.Value.MemoryType;

        public string FileName => Symbol.FileName;

        public uint LineNumber => Symbol.LineNumber;

        public string AddressText => FormatAddress(Symbol.Value);


        public string ValueText => AgcWordFormatter.FormatValue(RawWords, ValueFormat);

        [ObservableProperty]
        public int wordCount = 1;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ValueText))]
        private ValueFormat valueFormat = ValueFormat.Octal;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ValueText))]
        private ushort[] rawWords = [];

        public IReadOnlyList<ValueFormat> AvailableFormats => Enum.GetValues<ValueFormat>();

        public WatchItemViewModel(Symbol symbol)
        {
            Symbol = symbol;
        }

        public void UpdateValues(ushort[] values)
        {
            RawWords = values;
        }

        private static string FormatAddress(SymbolValue value)
        {
            if (value.Kind == ValueKind.Constant)
                return $"={Convert.ToString(value.Value, 8)}";

            if (value.Kind != ValueKind.Address)
                return "-";

            string sreg = Convert.ToString(value.SReg, 8).PadLeft(4, '0');

            if (value.BankingMode.HasFlag(BankingMode.Banked))
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

        private static int WordsPerValue(ValueFormat format)
        {
            return format switch
            {
                ValueFormat.Octal => 1,
                ValueFormat.Decimal => 1,
                ValueFormat.SP => 1,
                ValueFormat.DP => 2,
                ValueFormat.TP => 3,

                _ => throw new ArgumentOutOfRangeException(nameof(format))
            };
        }

        
    }
}
