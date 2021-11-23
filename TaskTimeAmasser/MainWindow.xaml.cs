using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TaskTimeAmasser
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public void Init()
        {
            ((MainWindowViewModel)this.DataContext).dialog = this.dialog;
        }


        private void DataGrid_ContextMenuItem_AllSelect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var item = (MenuItem)sender;
                var cmenu = (ContextMenu)item.Parent;
                var target = (DataGrid)cmenu.PlacementTarget;
                target.SelectAll();
                target.UpdateLayout();
                target.Focus();
            }
            catch
            {
                // 何もしない
            }
        }

        private void DataGrid_ContextMenuItem_CopyToClipboardIncludeHeader_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var item = (MenuItem)sender;
                var cmenu = (ContextMenu)item.Parent;
                var target = (DataGrid)cmenu.PlacementTarget;
                var mode = target.ClipboardCopyMode;
                target.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
                ApplicationCommands.Copy.Execute(null, target);
                target.ClipboardCopyMode = mode;
            }
            catch
            {
                // 何もしない
            }
        }

        private void DataGrid_ContextMenuItem_CopyToClipboardExcludeHeader_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var item = (MenuItem)sender;
                var cmenu = (ContextMenu)item.Parent;
                var target = (DataGrid)cmenu.PlacementTarget;
                var mode = target.ClipboardCopyMode;
                target.ClipboardCopyMode = DataGridClipboardCopyMode.ExcludeHeader;
                ApplicationCommands.Copy.Execute(null, target);
                target.ClipboardCopyMode = mode;
            }
            catch
            {
                // 何もしない
            }
        }
    }
}
