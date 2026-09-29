using AGCScope.ViewModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace AGCScope
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void btn_LoadRope_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            dlg.DefaultExt = ".bin";
            dlg.Filter = "AGC Core Rope File (*.bin)|*.bin";

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                string path = dlg.FileName;
                // Trim to only file name
                string filename = Path.GetFileName(path);
                lbl_LoadRope.Content = filename;

                ViewModel.LoadCoreRopeMemory(path);
            }
        }

        private MainViewModel ViewModel => (MainViewModel)DataContext;

        private void btn_LoadSymtab_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            dlg.DefaultExt = ".symtab";
            dlg.Filter = "yaYUL Symbol Table File (*.symtab)|*.symtab";

            Nullable<bool> result = dlg.ShowDialog();

            if(result == true)
            {
                string path = dlg.FileName;
                string filename = Path.GetFileName(path);
                lbl_LoadSymtab.Content = filename;

                ViewModel.LoadSymtab(path);
            }
        }

        private void tb_SymbolSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ViewModel.IsSearchOpen)
            {
                switch (e.Key)
                {
                    case Key.Down:
                        if (lv_SearchResults.Items.Count == 0)
                            return;

                        if (lv_SearchResults.SelectedIndex < 0)
                            lv_SearchResults.SelectedIndex = 0;
                        else if (lv_SearchResults.SelectedIndex < lv_SearchResults.Items.Count - 1)
                            lv_SearchResults.SelectedIndex++;

                        lv_SearchResults.ScrollIntoView(lv_SearchResults.SelectedItem);
                        e.Handled = true;
                        break;

                    case Key.Up:
                        if (lv_SearchResults.Items.Count == 0)
                            return;

                        if (lv_SearchResults.SelectedIndex > 0)
                            lv_SearchResults.SelectedIndex--;

                        lv_SearchResults.ScrollIntoView(lv_SearchResults.SelectedItem);
                        e.Handled = true;
                        break;

                    case Key.Enter:
                        if (ViewModel.AddWatchCommand.CanExecute(null))
                            ViewModel.AddWatchCommand.Execute(null);

                        e.Handled = true;
                        break;

                    case Key.Escape:
                        ViewModel.IsSearchOpen = false;
                        e.Handled = true;
                        break;
                }
            }
        }

        private void lv_SearchResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel.AddWatchCommand.CanExecute(null))
                ViewModel.AddWatchCommand.Execute(null);
        }
    }
}