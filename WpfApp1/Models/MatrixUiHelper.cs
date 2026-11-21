using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfApp1.Models
{
    internal static class MatrixUiHelper
    {
        public static void ClearState()
        {
            int r = TransportState.Rows;
            int c = TransportState.Cols;
            TransportState.Costs = new double[r, c];
            TransportState.Supply = new double[r];
            TransportState.Demand = new double[c];
        }

        public static void BuildInputMatrix(
            Grid grid, int rows, int cols, Style cellStyle,
            out TextBox[,] costBoxes, out TextBox[] supplyBoxes, out TextBox[] demandBoxes)
        {
            grid.Children.Clear();
            grid.RowDefinitions.Clear();
            grid.ColumnDefinitions.Clear();

            costBoxes = new TextBox[rows, cols];
            supplyBoxes = new TextBox[rows];
            demandBoxes = new TextBox[cols];

            // Единая сетка всех полей ввода для навигации стрелками (без правого нижнего угла)
            var navGrid = new TextBox[rows + 1, cols + 1];

            ResizeStateIfNeeded(rows, cols);

            for (int i = 0; i < rows + 2; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int j = 0; j < cols + 2; j++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            AddHeaderCell(grid, "Поставщик \\ Потребитель", 0, 0, "#F9FAFB");
            for (int j = 0; j < cols; j++) AddHeaderCell(grid, $"B{j + 1}", 0, j + 1, "#F9FAFB");
            AddHeaderCell(grid, "Запасы", 0, cols + 1, "#EFF6FF", "#1D4ED8");

            for (int i = 0; i < rows; i++)
            {
                AddHeaderCell(grid, $"A{i + 1}", i + 1, 0, "#F9FAFB");
                for (int j = 0; j < cols; j++)
                {
                    int rIdx = i, cIdx = j;
                    double val = TransportState.Costs[rIdx, cIdx];
                    var tb = new TextBox { Style = cellStyle, Text = val.ToString(CultureInfo.InvariantCulture) };
                    tb.TextChanged += (s, e) =>
                    {
                        if (TryParseNum(tb.Text, out double v))
                            TransportState.Costs[rIdx, cIdx] = v;
                    };
                    AttachCellBehavior(tb, navGrid, i, j);
                    Grid.SetRow(tb, i + 1);
                    Grid.SetColumn(tb, j + 1);
                    grid.Children.Add(tb);
                    costBoxes[i, j] = tb;
                    navGrid[i, j] = tb;
                }

                int sIdx = i;
                double sVal = TransportState.Supply[sIdx];
                var sTb = new TextBox
                {
                    Style = cellStyle,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC")),
                    FontWeight = FontWeights.SemiBold,
                    Text = sVal.ToString(CultureInfo.InvariantCulture)
                };
                sTb.TextChanged += (s, e) =>
                {
                    if (TryParseNum(sTb.Text, out double v))
                        TransportState.Supply[sIdx] = v;
                };
                AttachCellBehavior(sTb, navGrid, i, cols);
                Grid.SetRow(sTb, i + 1);
                Grid.SetColumn(sTb, cols + 1);
                grid.Children.Add(sTb);
                supplyBoxes[i] = sTb;
                navGrid[i, cols] = sTb;
            }

            AddHeaderCell(grid, "Потребности", rows + 1, 0, "#EFF6FF", "#1D4ED8");
            for (int j = 0; j < cols; j++)
            {
                int dIdx = j;
                double dVal = TransportState.Demand[dIdx];
                var dTb = new TextBox
                {
                    Style = cellStyle,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC")),
                    FontWeight = FontWeights.SemiBold,
                    Text = dVal.ToString(CultureInfo.InvariantCulture)
                };
                dTb.TextChanged += (s, e) =>
                {
                    if (TryParseNum(dTb.Text, out double v))
                        TransportState.Demand[dIdx] = v;
                };
                AttachCellBehavior(dTb, navGrid, rows, j);
                Grid.SetRow(dTb, rows + 1);
                Grid.SetColumn(dTb, j + 1);
                grid.Children.Add(dTb);
                demandBoxes[j] = dTb;
                navGrid[rows, j] = dTb;
            }
        }

        private static void AttachCellBehavior(TextBox tb, TextBox[,] navGrid, int r, int c)
        {
            tb.GotKeyboardFocus += (s, e) => tb.SelectAll();
            tb.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (!tb.IsKeyboardFocusWithin)
                {
                    e.Handled = true;
                    tb.Focus();
                }
            };

            tb.PreviewKeyDown += (s, e) =>
            {
                int maxR = navGrid.GetLength(0) - 1;
                int maxC = navGrid.GetLength(1) - 1;
                int nr = r, nc = c;

                if (e.Key == Key.Up) nr--;
                else if (e.Key == Key.Down || e.Key == Key.Enter) nr++;
                else if (e.Key == Key.Left) nc--;
                else if (e.Key == Key.Right) nc++;
                else return;

                if (nr >= 0 && nr <= maxR && nc >= 0 && nc <= maxC && navGrid[nr, nc] != null)
                {
                    navGrid[nr, nc].Focus();
                    e.Handled = true;
                }
            };
        }

        private static void ResizeStateIfNeeded(int rows, int cols)
        {
            if (TransportState.Costs.GetLength(0) == rows && TransportState.Costs.GetLength(1) == cols)
                return;

            var newCosts = new double[rows, cols];
            var newSupply = new double[rows];
            var newDemand = new double[cols];

            for (int i = 0; i < rows; i++)
            {
                if (i < TransportState.Supply.Length) newSupply[i] = TransportState.Supply[i];
                for (int j = 0; j < cols; j++)
                {
                    if (i < TransportState.Costs.GetLength(0) && j < TransportState.Costs.GetLength(1))
                        newCosts[i, j] = TransportState.Costs[i, j];
                }
            }
            for (int j = 0; j < cols; j++)
            {
                if (j < TransportState.Demand.Length) newDemand[j] = TransportState.Demand[j];
            }

            TransportState.Rows = rows;
            TransportState.Cols = cols;
            TransportState.Costs = newCosts;
            TransportState.Supply = newSupply;
            TransportState.Demand = newDemand;
        }

        public static bool TryReadAndSaveState(
            TextBox[,] costBoxes, TextBox[] supplyBoxes, TextBox[] demandBoxes,
            out double[] supply, out double[] demand, out double[,] costs)
        {
            int rows = costBoxes.GetLength(0);
            int cols = costBoxes.GetLength(1);
            supply = new double[rows];
            demand = new double[cols];
            costs = new double[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                if (!TryParseNum(supplyBoxes[i].Text, out supply[i])) return false;
                for (int j = 0; j < cols; j++)
                    if (!TryParseNum(costBoxes[i, j].Text, out costs[i, j])) return false;
            }
            for (int j = 0; j < cols; j++)
                if (!TryParseNum(demandBoxes[j].Text, out demand[j])) return false;

            TransportState.Rows = rows;
            TransportState.Cols = cols;
            TransportState.Costs = costs;
            TransportState.Supply = supply;
            TransportState.Demand = demand;
            return true;
        }

        public static void RenderResultMatrix(Grid grid, TransportResult res)
        {
            grid.Children.Clear();
            grid.RowDefinitions.Clear();
            grid.ColumnDefinitions.Clear();

            int rows = res.BalancedSupply.Length, cols = res.BalancedDemand.Length;
            for (int i = 0; i < rows + 2; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int j = 0; j < cols + 2; j++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            AddHeaderCell(grid, "План", 0, 0, "#F9FAFB");
            for (int j = 0; j < cols; j++) AddHeaderCell(grid, $"B{j + 1}", 0, j + 1, "#F9FAFB");
            AddHeaderCell(grid, "Запасы", 0, cols + 1, "#F9FAFB");

            for (int i = 0; i < rows; i++)
            {
                AddHeaderCell(grid, $"A{i + 1}", i + 1, 0, "#F9FAFB");
                for (int j = 0; j < cols; j++)
                {
                    double x = res.Allocation[i, j], c = res.BalancedCosts[i, j];
                    bool active = x > 1e-6;

                    var border = new Border
                    {
                        Width = 72,
                        Height = 44,
                        Margin = new Thickness(2),
                        CornerRadius = new CornerRadius(4),
                        BorderThickness = new Thickness(1),
                        BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(active ? "#93C5FD" : "#E5E7EB")),
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(active ? "#EFF6FF" : "#FFFFFF"))
                    };

                    var g = new Grid();
                    g.Children.Add(new TextBlock
                    {
                        Text = c.ToString("0.##"),
                        FontSize = 10.5,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, 2, 5, 0)
                    });
                    g.Children.Add(new TextBlock
                    {
                        Text = active ? x.ToString("0.##") : "—",
                        FontSize = 13.5,
                        FontWeight = active ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(active ? "#1E40AF" : "#9CA3AF")),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 6, 0, 0)
                    });
                    border.Child = g;
                    Grid.SetRow(border, i + 1);
                    Grid.SetColumn(border, j + 1);
                    grid.Children.Add(border);
                }
                AddHeaderCell(grid, res.BalancedSupply[i].ToString("0.##"), i + 1, cols + 1, "#F9FAFB");
            }
            AddHeaderCell(grid, "Потребности", rows + 1, 0, "#F9FAFB");
            for (int j = 0; j < cols; j++) AddHeaderCell(grid, res.BalancedDemand[j].ToString("0.##"), rows + 1, j + 1, "#F9FAFB");
        }
        private static bool TryParseNum(string text, out double val)
        {
            text = text?.Trim().Replace(',', '.');
            return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out val) && val >= 0;
        }
        private static void AddHeaderCell(Grid grid, string text, int row, int col, string bgHex, string fgHex = "#374151")
        {
            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex)),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E7EB")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(2),
                Padding = new Thickness(10, 6, 10, 6),
                MinWidth = 58
            };
            border.Child = new TextBlock
            {
                Text = text,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fgHex)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            grid.Children.Add(border);
        }
    }
}
