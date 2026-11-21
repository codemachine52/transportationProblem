using System;
using System.Collections.Generic;
using System.Globalization;
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
using WpfApp1.Models;

namespace WpfApp1.Pages
{
    /// <summary>
    /// Логика взаимодействия для MinElementPage.xaml
    /// </summary>
    public partial class MinElementPage : Page
    {
        private TextBox[,] _costBoxes;
        private TextBox[] _supplyBoxes;
        private TextBox[] _demandBoxes;

        public MinElementPage()
        {
            InitializeComponent();
            RowsBox.Text = TransportState.Rows.ToString();
            ColsBox.Text = TransportState.Cols.ToString();
            MatrixUiHelper.BuildInputMatrix(InputMatrixGrid, TransportState.Rows, TransportState.Cols,
                (Style)FindResource("CellTextBox"), out _costBoxes, out _supplyBoxes, out _demandBoxes);
        }

        private void BuildGrid_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(RowsBox.Text, out int r) || !int.TryParse(ColsBox.Text, out int c) || r < 1 || c < 1 || r > 15 || c > 15)
            {
                MessageBox.Show("Введите размеры от 1 до 15.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            MatrixUiHelper.TryReadAndSaveState(_costBoxes, _supplyBoxes, _demandBoxes, out _, out _, out _);
            TransportState.Rows = r;
            TransportState.Cols = c;
            ResultCard.Visibility = Visibility.Collapsed;
            MatrixUiHelper.BuildInputMatrix(InputMatrixGrid, r, c,
                (Style)FindResource("CellTextBox"), out _costBoxes, out _supplyBoxes, out _demandBoxes);
        }

        private void Solve_Click(object sender, RoutedEventArgs e)
        {
            if (!MatrixUiHelper.TryReadAndSaveState(_costBoxes, _supplyBoxes, _demandBoxes, out var s, out var d, out var c))
            {
                MessageBox.Show("Проверьте корректность чисел в таблице.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var res = TransportSolver.Solve(s, d, c, useMinElement: true, optimizeModi: false);
            ResultCard.Visibility = Visibility.Visible;
            TotalCostText.Text = $"Целевая функция F = {res.TotalCost:0.##}";
            BalanceInfoText.Text = res.InfoNote;
            MatrixUiHelper.RenderResultMatrix(OutputMatrixGrid, res);
        }
        private void ClearGrid_Click(object sender, RoutedEventArgs e)
        {
            MatrixUiHelper.ClearState();
            ResultCard.Visibility = Visibility.Collapsed;
            MatrixUiHelper.BuildInputMatrix(InputMatrixGrid, TransportState.Rows, TransportState.Cols,
                (Style)FindResource("CellTextBox"), out _costBoxes, out _supplyBoxes, out _demandBoxes);
        }
    }
}
