using System;
using System.Collections.Generic;
using System.Linq;

namespace WpfApp1.Models
{
    public class TransportResult
    {
        public double[,] InitialAllocation { get; set; } = new double[0, 0];
        public double[,] Allocation { get; set; } = new double[0, 0];
        public double[,] BalancedCosts { get; set; } = new double[0, 0];
        public double[] BalancedSupply { get; set; } = Array.Empty<double>();
        public double[] BalancedDemand { get; set; } = Array.Empty<double>();
        public double InitialCost { get; set; }
        public double TotalCost { get; set; }
        public string InfoNote { get; set; } = string.Empty;
        public int OptimizationIterations { get; set; }
        public bool IsOptimized { get; set; }
        public int DummyRowIndex { get; set; } = -1;
        public int DummyColIndex { get; set; } = -1;
    }

    internal static class TransportSolver
    {
        private const double Eps = 1e-7;

        public static TransportResult Solve(double[] supply, double[] demand, double[,] costs, bool useMinElement, bool optimizeModi)
        {
            var (s, d, c, note, dummyRow, dummyCol) = Balance(supply, demand, costs);
            int m = s.Length, n = d.Length;

            var basis = new bool[m, n];
            var alloc = useMinElement
                ? BuildMinElement(s, d, c, basis, dummyRow, dummyCol)
                : BuildNorthWest(s, d, basis);

            var initialAlloc = (double[,])alloc.Clone();
            double initialCost = CalcCost(alloc, c);
            int iterations = 0;

            if (optimizeModi)
            {
                iterations = OptimizeByPotentials(alloc, c, basis);
            }

            double finalCost = CalcCost(alloc, c);
            string optText = optimizeModi
                ? $" Оптимизация методом потенциалов: итераций — {iterations}, улучшение с {initialCost:0.##} до {finalCost:0.##}."
                : "";

            return new TransportResult
            {
                InitialAllocation = initialAlloc,
                Allocation = alloc,
                BalancedCosts = c,
                BalancedSupply = s,
                BalancedDemand = d,
                InitialCost = initialCost,
                TotalCost = finalCost,
                OptimizationIterations = iterations,
                IsOptimized = optimizeModi,
                DummyRowIndex = dummyRow,
                DummyColIndex = dummyCol,
                InfoNote = note + optText
            };
        }

        private static (double[] s, double[] d, double[,] c, string note, int dummyRow, int dummyCol) Balance(
            double[] supply, double[] demand, double[,] costs)
        {
            double sumS = supply.Sum(), sumD = demand.Sum();
            int m = supply.Length, n = demand.Length;

            if (Math.Abs(sumS - sumD) < Eps)
                return ((double[])supply.Clone(), (double[])demand.Clone(), (double[,])costs.Clone(), "Задача закрытая.", -1, -1);

            if (sumS > sumD)
            {
                double diff = sumS - sumD;
                var nd = new double[n + 1];
                Array.Copy(demand, nd, n);
                nd[n] = diff;
                var nc = new double[m, n + 1];
                for (int i = 0; i < m; i++)
                    for (int j = 0; j < n; j++) nc[i, j] = costs[i, j];
                return ((double[])supply.Clone(), nd, nc, $"Добавлен фиктивный потребитель B{n + 1} ({diff:0.##}).", -1, n);
            }
            else
            {
                double diff = sumD - sumS;
                var ns = new double[m + 1];
                Array.Copy(supply, ns, m);
                ns[m] = diff;
                var nc = new double[m + 1, n];
                for (int i = 0; i < m; i++)
                    for (int j = 0; j < n; j++) nc[i, j] = costs[i, j];
                return (ns, (double[])demand.Clone(), nc, $"Добавлен фиктивный поставщик A{m + 1} ({diff:0.##}).", m, -1);
            }
        }

        private static double[,] BuildNorthWest(double[] s, double[] d, bool[,] basis)
        {
            int m = s.Length, n = d.Length;
            var remS = (double[])s.Clone();
            var remD = (double[])d.Clone();
            var alloc = new double[m, n];

            int i = 0, j = 0;
            while (i < m && j < n)
            {
                double x = Math.Min(remS[i], remD[j]);
                alloc[i, j] = x;
                basis[i, j] = true;
                remS[i] -= x;
                remD[j] -= x;

                if (Math.Abs(remS[i]) < Eps && Math.Abs(remD[j]) < Eps)
                {
                    if (j + 1 < n) basis[i, j + 1] = true;
                    else if (i + 1 < m) basis[i + 1, j] = true;
                    i++; j++;
                }
                else if (Math.Abs(remS[i]) < Eps) i++;
                else j++;
            }
            return alloc;
        }

        private static double[,] BuildMinElement(double[] s, double[] d, double[,] c, bool[,] basis, int dummyRow, int dummyCol)
        {
            int m = s.Length, n = d.Length;
            var remS = (double[])s.Clone();
            var remD = (double[])d.Clone();
            var alloc = new double[m, n];
            var rClosed = new bool[m];
            var cClosed = new bool[n];
            int openRows = m, openCols = n;

            for (int step = 0; step < m + n - 1; step++)
            {
                int bi = -1, bj = -1;
                double minC = double.MaxValue;

                // Сначала ищем минимальный тариф среди реальных (не фиктивных) клеток
                for (int i = 0; i < m; i++)
                {
                    if (rClosed[i] || i == dummyRow) continue;
                    for (int j = 0; j < n; j++)
                    {
                        if (cClosed[j] || j == dummyCol) continue;
                        if (c[i, j] < minC) { minC = c[i, j]; bi = i; bj = j; }
                    }
                }

                // Если реальных открытых клеток не осталось — заполняем фиктивные
                if (bi == -1)
                {
                    for (int i = 0; i < m; i++)
                    {
                        if (rClosed[i]) continue;
                        for (int j = 0; j < n; j++)
                        {
                            if (cClosed[j]) continue;
                            if (c[i, j] < minC) { minC = c[i, j]; bi = i; bj = j; }
                        }
                    }
                }

                if (bi == -1) break;

                double x = Math.Min(remS[bi], remD[bj]);
                alloc[bi, bj] = x;
                basis[bi, bj] = true;
                remS[bi] -= x;
                remD[bj] -= x;

                bool rowZero = Math.Abs(remS[bi]) < Eps;
                bool colZero = Math.Abs(remD[bj]) < Eps;

                if (rowZero && colZero)
                {
                    // Закрываем ту линию, которых осталось больше, чтобы не прервать цикл раньше времени
                    if (openRows >= openCols && openRows > 1)
                    {
                        rClosed[bi] = true;
                        openRows--;
                    }
                    else if (openCols > 1)
                    {
                        cClosed[bj] = true;
                        openCols--;
                    }
                    else
                    {
                        rClosed[bi] = true;
                        openRows--;
                    }
                }
                else if (rowZero)
                {
                    rClosed[bi] = true;
                    openRows--;
                }
                else
                {
                    cClosed[bj] = true;
                    openCols--;
                }
            }
            return alloc;
        }

        private static int OptimizeByPotentials(double[,] alloc, double[,] c, bool[,] basis)
        {
            int m = alloc.GetLength(0), n = alloc.GetLength(1);
            int iter = 0;

            while (iter < 1000)
            {
                EnsureNonDegenerate(basis, c);

                var u = new double?[m];
                var v = new double?[n];
                u[0] = 0;

                bool changed = true;
                while (changed)
                {
                    changed = false;
                    for (int i = 0; i < m; i++)
                        for (int j = 0; j < n; j++)
                        {
                            if (!basis[i, j]) continue;
                            if (u[i].HasValue && !v[j].HasValue) { v[j] = c[i, j] - u[i].Value; changed = true; }
                            else if (v[j].HasValue && !u[i].HasValue) { u[i] = c[i, j] - v[j].Value; changed = true; }
                        }
                }

                int enterI = -1, enterJ = -1;
                double minDelta = -Eps;

                for (int i = 0; i < m; i++)
                    for (int j = 0; j < n; j++)
                    {
                        if (basis[i, j] || !u[i].HasValue || !v[j].HasValue) continue;
                        double delta = c[i, j] - (u[i].Value + v[j].Value);
                        if (delta < minDelta)
                        {
                            minDelta = delta;
                            enterI = i;
                            enterJ = j;
                        }
                    }

                if (enterI == -1) break; // План оптимален

                var cycle = FindCycle(basis, enterI, enterJ);
                if (cycle == null || cycle.Count < 4) break;

                double theta = double.MaxValue;
                int leaveIdx = -1;
                for (int k = 1; k < cycle.Count; k += 2)
                {
                    var (ci, cj) = cycle[k];
                    if (alloc[ci, cj] < theta)
                    {
                        theta = alloc[ci, cj];
                        leaveIdx = k;
                    }
                }

                for (int k = 0; k < cycle.Count; k++)
                {
                    var (ci, cj) = cycle[k];
                    alloc[ci, cj] += (k % 2 == 0) ? theta : -theta;
                    if (Math.Abs(alloc[ci, cj]) < Eps) alloc[ci, cj] = 0;
                }

                basis[enterI, enterJ] = true;
                var (li, lj) = cycle[leaveIdx];
                basis[li, lj] = false;
                iter++;
            }

            return iter;
        }

        private static void EnsureNonDegenerate(bool[,] basis, double[,] c)
        {
            int m = basis.GetLength(0), n = basis.GetLength(1);
            int count = 0;
            for (int i = 0; i < m; i++)
                for (int j = 0; j < n; j++) if (basis[i, j]) count++;

            while (count < m + n - 1)
            {
                int bi = -1, bj = -1;
                double minC = double.MaxValue;
                for (int i = 0; i < m; i++)
                    for (int j = 0; j < n; j++)
                    {
                        if (basis[i, j]) continue;
                        if (c[i, j] < minC && FindCycle(basis, i, j) == null)
                        {
                            minC = c[i, j]; bi = i; bj = j;
                        }
                    }
                if (bi == -1) break;
                basis[bi, bj] = true;
                count++;
            }
        }

        private static List<(int r, int c)>? FindCycle(bool[,] basis, int startR, int startC)
        {
            int m = basis.GetLength(0), n = basis.GetLength(1);
            var path = new List<(int r, int c)> { (startR, startC) };

            // Начинаем движение по строке (горизонтально) и замыкаем цикл строго по столбцу (вертикально),
            // что гарантирует чередование направлений и чётную длину цикла.
            bool Dfs(int r, int c, bool moveRow)
            {
                if (moveRow)
                {
                    for (int nc = 0; nc < n; nc++)
                    {
                        if (nc == c) continue;
                        if (basis[r, nc] && !path.Contains((r, nc)))
                        {
                            path.Add((r, nc));
                            if (Dfs(r, nc, false)) return true;
                            path.RemoveAt(path.Count - 1);
                        }
                    }
                }
                else
                {
                    for (int nr = 0; nr < m; nr++)
                    {
                        if (nr == r) continue;
                        if (nr == startR && c == startC && path.Count >= 4 && path.Count % 2 == 0)
                            return true;
                        if (basis[nr, c] && !path.Contains((nr, c)))
                        {
                            path.Add((nr, c));
                            if (Dfs(nr, c, true)) return true;
                            path.RemoveAt(path.Count - 1);
                        }
                    }
                }
                return false;
            }

            return Dfs(startR, startC, true) ? path : null;
        }

        private static double CalcCost(double[,] alloc, double[,] c)
        {
            double sum = 0;
            for (int i = 0; i < alloc.GetLength(0); i++)
                for (int j = 0; j < alloc.GetLength(1); j++)
                    sum += alloc[i, j] * c[i, j];
            return sum;
        }
    }
}
