using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfApp1.Models;
using WpfApp1.Pages;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            MainFrame.Navigate(new NorthWestPage());
        }

        private void NavNorthWest_Checked(object sender, RoutedEventArgs e) => MainFrame?.Navigate(new NorthWestPage());
        private void NavMinElement_Checked(object sender, RoutedEventArgs e) => MainFrame?.Navigate(new MinElementPage());
        private void NavPotentials_Checked(object sender, RoutedEventArgs e) => MainFrame?.Navigate(new PotentialsPage());
    }
}