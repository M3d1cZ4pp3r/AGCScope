using AGCScope.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AGCScope.ViewModel
{
    public partial class MainViewModel : ObservableObject
    {
        public void LoadCoreRopeMemory(string path)
        {
            Image = Services.RopeLoader.Load(path);
        }

        public void LoadSymtab(string path)
        {
            symbolTable = Services.SymtabLoader.Load(path);
        }

        [ObservableProperty]
        private string searchText = "";

        [ObservableProperty]
        private bool isSearchOpen = false;

        [ObservableProperty]
        private string connectionStatus = "Disconnected";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddWatchCommand))]
        private SymbolSearchItemViewModel? selectedSearchResult;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveWatchCommand))]
        private WatchItemViewModel? selectedWatch;

        public ObservableCollection<SymbolSearchItemViewModel> SearchResults { get; } = new();
        public ObservableCollection<WatchItemViewModel> WatchedSymbols { get; } = new();

        private SymbolTable symbolTable = new SymbolTable();

        private NASSPAgcProvider agc = new NASSPAgcProvider();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConnectToNASSPCommand))]
        private RopeImage image = null!;

        private CancellationTokenSource? refreshCts;
        private Task? refreshTask;

        private bool CanConnect => Image is not null;
        private bool CanAddWatch => SelectedSearchResult is not null;
        private bool CanRemoveWatch => SelectedWatch is not null;

        public void StartRefresh()
        {
            if (refreshTask is not null)
                return;

            refreshCts = new CancellationTokenSource();
            refreshTask = RefreshLoopAsync(refreshCts.Token);
        }

        public async Task StopRefreshAsync()
        {
            if (refreshCts is null)
                return;

            refreshCts.Cancel();

            try
            {
                if (refreshTask is not null)
                    await refreshTask;
            }
            catch(OperationCanceledException)
            {
            }

            refreshCts.Dispose();
            refreshCts = null;
            refreshTask = null;
        }

        private async Task RefreshLoopAsync(CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));

            while(await timer.WaitForNextTickAsync(cancellationToken))
            {
                agc.RefreshMemory();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    UpdateWatchedSymbols();
                    CheckConnection();

                    if (!agc.IsConnected()) return;
                    cancellationToken.ThrowIfCancellationRequested();
                });
            }
        }

        partial void OnSearchTextChanged(string? oldValue, string newValue)
        {
            UpdateSearchResults();
        }

        private void UpdateSearchResults()
        {
            SearchResults.Clear();

            if(string.IsNullOrWhiteSpace(SearchText))
            {
                IsSearchOpen = false;
                return;
            }

            var matches = symbolTable.Symbols
                .Where(s =>
                    s.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .Where(s => s.Type != SymbolType.Label)
                .OrderByDescending(s =>
                    s.Name.StartsWith(SearchText, StringComparison.OrdinalIgnoreCase))
                .ThenBy(s => s.Name)
                .Take(30);

            foreach (var symbol in matches)
                SearchResults.Add(new SymbolSearchItemViewModel(symbol));

            IsSearchOpen = SearchResults.Count > 0;
        }

        [RelayCommand(CanExecute = nameof(CanAddWatch))]
        private void AddWatch()
        {
            if (SelectedSearchResult is null)
                return;
            if (WatchedSymbols.Any(x => x.Symbol == SelectedSearchResult.Symbol))
                return;

            WatchedSymbols.Add(new WatchItemViewModel(SelectedSearchResult.Symbol));
            UpdateWatchedSymbols();
        }

        [RelayCommand(CanExecute = nameof(CanRemoveWatch))]
        private void RemoveWatch()
        {
            if (SelectedWatch is null)
                return;

            WatchedSymbols.Remove(SelectedWatch);
        }

        [RelayCommand(CanExecute = nameof(CanConnect))]
        public async Task ConnectToNASSP()
        {
            if(Image is not null)
            {
                ConnectionStatus = "Connecting...";
                await Task.Run(() => agc.Connect(Image));
                CheckConnection();
                UpdateWatchedSymbols();

                StartRefresh();
            }
            else
            {
                MessageBox.Show("Load a rope image first");
            }
        }

        private void CheckConnection()
        {
            ConnectionStatus = agc.IsConnected() ? "Connected" : "Disconnected";
        }

        private void UpdateWatchedSymbols()
        {
            foreach(var symbol in WatchedSymbols)
            {
                SymbolValue value = symbol.Symbol.Value;
                if(value.MemoryType == MemoryType.Erasable && value.Kind == ValueKind.Address)
                {
                    const uint BankSize = 256;
                    const uint BankedBase = 768;
                    const uint ErasableEnd = 1023;

                    uint bank, offset;

                    uint sreg = value.SReg;

                    // Needs to be in erasable memory (1024 words)
                    if (sreg < 0 || sreg > ErasableEnd)
                        throw new ArgumentOutOfRangeException(nameof(value));

                    // Unbanked erasable: 0000 - 1377
                    if(sreg < BankedBase)
                    {
                        bank = sreg / BankSize;
                        offset = sreg % BankSize;
                    }
                    else
                    {
                        // Banked erasable: 1400 - 1777
                        if (!value.Bank.HasValue)
                            throw new System.IO.InvalidDataException("Banked erasable address has no EB value.");

                        bank = value.Bank.Value;
                        offset = sreg - BankedBase;
                    }

                    ushort[] words = new ushort[symbol.WordCount];

                    for(int i = 0; i < symbol.WordCount; i++)
                    {
                        var address = offset + (uint)i;
                        if (address >= BankSize)
                        {
                            // TODO: Message or replacement text
                            break;
                        }

                        words[i] = agc.Read(bank, address);
                    }

                    symbol.UpdateValues(words);
                }
            }
        }
    }
}
