using System.Windows;
using System.Windows.Controls;
using WpfApp1.Models;

namespace WpfApp1.Pages
{
    public partial class PotentialsPage : Page
    {
        private TextBox[,] _costBoxes;
        private TextBox[] _supplyBoxes;
        private TextBox[] _demandBoxes;
        private TransportResult? _lastResult;
        private string _lastBasePlanName = "Минимальный элемент";

        public PotentialsPage()
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
            _lastResult = null;
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
            bool useMinElement = BasePlanCombo.SelectedIndex == 0;
            _lastBasePlanName = useMinElement ? "Минимальный элемент" : "Северо-западный угол";
            _lastResult = TransportSolver.Solve(s, d, c, useMinElement: useMinElement, optimizeModi: true);
            ResultCard.Visibility = Visibility.Visible;
            TotalCostText.Text = $"Оптимальная стоимость F = {_lastResult.TotalCost:0.##}";
            BalanceInfoText.Text = _lastResult.InfoNote;
            MatrixUiHelper.RenderResultMatrix(OutputMatrixGrid, _lastResult);
        }

        private void ClearGrid_Click(object sender, RoutedEventArgs e)
        {
            MatrixUiHelper.ClearState();
            _lastResult = null;
            ResultCard.Visibility = Visibility.Collapsed;
            MatrixUiHelper.BuildInputMatrix(InputMatrixGrid, TransportState.Rows, TransportState.Cols,
                (Style)FindResource("CellTextBox"), out _costBoxes, out _supplyBoxes, out _demandBoxes);
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            ExcelExporter.ExportWithDialog(_lastResult, $"Метод потенциалов (опорный: {_lastBasePlanName})", "Метод_потенциалов");
        }
    }
}
