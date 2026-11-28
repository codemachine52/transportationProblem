using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace WpfApp1.Models
{
    internal static class ExcelExporter
    {
        public static void ExportWithDialog(TransportResult? result, string methodTitle, string defaultFileName)
        {
            if (result == null)
            {
                MessageBox.Show("Сначала выполните расчёт.", "Экспорт в Excel", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Сохранить результат в Excel",
                Filter = "Файлы Excel (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                FileName = $"{defaultFileName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                ExportToXlsx(dialog.FileName, result, methodTitle);
                MessageBox.Show($"Файл успешно сохранён:\n{dialog.FileName}", "Экспорт в Excel", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось сохранить файл:\n{ex.Message}", "Ошибка экспорта", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void ExportToXlsx(string filePath, TransportResult res, string methodTitle)
        {
            string sheetXml = BuildWorksheetXml(res, methodTitle);

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

            WriteEntry(zip, "[Content_Types].xml", ContentTypesXml);
            WriteEntry(zip, "_rels/.rels", RootRelsXml);
            WriteEntry(zip, "xl/workbook.xml", WorkbookXml);
            WriteEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRelsXml);
            WriteEntry(zip, "xl/styles.xml", StylesXml);
            WriteEntry(zip, "xl/worksheets/sheet1.xml", sheetXml);
        }

        private static string BuildWorksheetXml(TransportResult res, string methodTitle)
        {
            int m = res.BalancedSupply.Length;
            int n = res.BalancedDemand.Length;
            int supplyCol = n + 2; // A = 1, B1..Bn = 2..n+1, Запасы = n+2

            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");

            // Ширина столбцов
            sb.Append("<cols>");
            sb.Append("<col min=\"1\" max=\"1\" width=\"24\" customWidth=\"1\"/>");
            sb.Append($"<col min=\"2\" max=\"{n + 1}\" width=\"12\" customWidth=\"1\"/>");
            sb.Append($"<col min=\"{supplyCol}\" max=\"{supplyCol}\" width=\"16\" customWidth=\"1\"/>");
            sb.Append($"<col min=\"{supplyCol + 1}\" max=\"{supplyCol + 1}\" width=\"24\" customWidth=\"1\"/>");
            sb.Append("</cols>");

            sb.Append("<sheetData>");
            int row = 1;

            // Заголовок документа
            sb.Append(RowStart(row));
            sb.Append(TextCell(1, row, $"Транспортная задача — {methodTitle}", style: 1));
            sb.Append("</row>");
            row++;

            sb.Append(RowStart(row));
            sb.Append(TextCell(1, row, res.InfoNote, style: 8));
            sb.Append("</row>");
            row += 2;

            // === ТАБЛИЦА 1: ИСХОДНЫЕ ДАННЫЕ (ТАРИФЫ) ===
            sb.Append(RowStart(row));
            sb.Append(TextCell(1, row, "1. Исходные данные (матрица тарифов)", style: 2));
            sb.Append("</row>");
            row++;

            sb.Append(RowStart(row));
            sb.Append(TextCell(1, row, "Поставщик \\ Потребитель", style: 3));
            for (int j = 0; j < n; j++)
                sb.Append(TextCell(j + 2, row, j == res.DummyColIndex ? $"B{j + 1} (фикт.)" : $"B{j + 1}", style: 3));
            sb.Append(TextCell(supplyCol, row, "Запасы", style: 3));
            sb.Append("</row>");
            row++;

            int costStartRow = row;
            for (int i = 0; i < m; i++)
            {
                sb.Append(RowStart(row));
                sb.Append(TextCell(1, row, i == res.DummyRowIndex ? $"A{i + 1} (фикт.)" : $"A{i + 1}", style: 4));
                for (int j = 0; j < n; j++)
                    sb.Append(NumCell(j + 2, row, res.BalancedCosts[i, j], style: 5));
                sb.Append(NumCell(supplyCol, row, res.BalancedSupply[i], style: 4));
                sb.Append("</row>");
                row++;
            }
            int costEndRow = row - 1;

            // Строка потребностей + контрольная сумма
            sb.Append(RowStart(row));
            sb.Append(TextCell(1, row, "Потребности", style: 4));
            double sumD = 0;
            for (int j = 0; j < n; j++)
            {
                sb.Append(NumCell(j + 2, row, res.BalancedDemand[j], style: 4));
                sumD += res.BalancedDemand[j];
            }
            string supplyColLetter = ColLetter(supplyCol);
            sb.Append(FormulaCell(supplyCol, row, $"SUM({supplyColLetter}{costStartRow}:{supplyColLetter}{costEndRow})", sumD, style: 4));
            sb.Append(TextCell(supplyCol + 1, row, "Суммарный объём", style: 8));
            sb.Append("</row>");
            row += 2;

            string costRange = $"{ColLetter(2)}{costStartRow}:{ColLetter(n + 1)}{costEndRow}";

            // === ТАБЛИЦА 2: ОПОРНЫЙ ПЛАН ===
            string plan2Title = res.IsOptimized
                ? "2. Опорный план перевозок (до оптимизации)"
                : "2. Опорный план перевозок";
            row = AppendPlanTable(sb, row, plan2Title, res.InitialAllocation, res, costRange, res.InitialCost, "Стоимость перевозок (опорный план)");

            // === ТАБЛИЦА 3: ОПТИМАЛЬНЫЙ ПЛАН (если решали методом потенциалов) ===
            if (res.IsOptimized)
            {
                row += 1;
                row = AppendPlanTable(sb, row, "3. Оптимальный план перевозок (метод потенциалов)", res.Allocation, res, costRange, res.TotalCost, "Оптимальная стоимость перевозок");

                sb.Append(RowStart(row));
                sb.Append(TextCell(1, row, "Итераций оптимизации:", style: 2));
                sb.Append(NumCell(2, row, res.OptimizationIterations, style: 5));
                sb.Append(TextCell(3, row, "Экономия:", style: 2));
                sb.Append(NumCell(4, row, res.InitialCost - res.TotalCost, style: 7));
                sb.Append("</row>");
            }

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static int AppendPlanTable(
            StringBuilder sb,
            int startRow,
            string sectionTitle,
            double[,] allocation,
            TransportResult res,
            string costRange,
            double totalCost,
            string costLabel)
        {
            int m = res.BalancedSupply.Length;
            int n = res.BalancedDemand.Length;
            int supplyCol = n + 2;
            int row = startRow;

            sb.Append(RowStart(row));
            sb.Append(TextCell(1, row, sectionTitle, style: 2));
            sb.Append("</row>");
            row++;

            sb.Append(RowStart(row));
            sb.Append(TextCell(1, row, "Поставщик \\ Потребитель", style: 3));
            for (int j = 0; j < n; j++)
                sb.Append(TextCell(j + 2, row, j == res.DummyColIndex ? $"B{j + 1} (фикт.)" : $"B{j + 1}", style: 3));
            sb.Append(TextCell(supplyCol, row, "Запасы (итого)", style: 3));
            sb.Append("</row>");
            row++;

            int allocStartRow = row;
            for (int i = 0; i < m; i++)
            {
                sb.Append(RowStart(row));
                sb.Append(TextCell(1, row, i == res.DummyRowIndex ? $"A{i + 1} (фикт.)" : $"A{i + 1}", style: 4));
                double rowSum = 0;
                for (int j = 0; j < n; j++)
                {
                    double x = allocation[i, j];
                    rowSum += x;
                    int cellStyle = x > 1e-6 ? 6 : 5;
                    sb.Append(NumCell(j + 2, row, x, style: cellStyle));
                }
                sb.Append(FormulaCell(supplyCol, row, $"SUM({ColLetter(2)}{row}:{ColLetter(n + 1)}{row})", rowSum, style: 4));
                sb.Append("</row>");
                row++;
            }
            int allocEndRow = row - 1;

            // Строка сумм по столбцам + итоговая стоимость перевозок
            sb.Append(RowStart(row));
            sb.Append(TextCell(1, row, "Потребности (итого)", style: 4));
            for (int j = 0; j < n; j++)
            {
                string colL = ColLetter(j + 2);
                sb.Append(FormulaCell(j + 2, row, $"SUM({colL}{allocStartRow}:{colL}{allocEndRow})", res.BalancedDemand[j], style: 4));
            }
            string allocRange = $"{ColLetter(2)}{allocStartRow}:{ColLetter(n + 1)}{allocEndRow}";
            sb.Append(FormulaCell(supplyCol, row, $"SUMPRODUCT({costRange},{allocRange})", totalCost, style: 7));
            sb.Append(TextCell(supplyCol + 1, row, costLabel, style: 2));
            sb.Append("</row>");
            row++;

            return row;
        }

        private static string RowStart(int r) => $"<row r=\"{r}\">";

        private static string TextCell(int col, int row, string text, int style)
        {
            string cellRef = $"{ColLetter(col)}{row}";
            string escaped = SecurityElement.Escape(text ?? string.Empty) ?? string.Empty;
            return $"<c r=\"{cellRef}\" s=\"{style}\" t=\"inlineStr\"><is><t>{escaped}</t></is></c>";
        }

        private static string NumCell(int col, int row, double value, int style)
        {
            string cellRef = $"{ColLetter(col)}{row}";
            string numStr = Math.Round(value, 6).ToString(CultureInfo.InvariantCulture);
            return $"<c r=\"{cellRef}\" s=\"{style}\"><v>{numStr}</v></c>";
        }

        private static string FormulaCell(int col, int row, string formula, double cachedValue, int style)
        {
            string cellRef = $"{ColLetter(col)}{row}";
            string escapedFormula = SecurityElement.Escape(formula) ?? string.Empty;
            string numStr = Math.Round(cachedValue, 6).ToString(CultureInfo.InvariantCulture);
            return $"<c r=\"{cellRef}\" s=\"{style}\"><f>{escapedFormula}</f><v>{numStr}</v></c>";
        }

        private static string ColLetter(int colIndex)
        {
            string res = string.Empty;
            while (colIndex > 0)
            {
                int rem = (colIndex - 1) % 26;
                res = (char)('A' + rem) + res;
                colIndex = (colIndex - 1) / 26;
            }
            return res;
        }

        private static void WriteEntry(ZipArchive zip, string entryName, string content)
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        private const string ContentTypesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "</Types>";

        private const string RootRelsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        private const string WorkbookXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<sheets><sheet name=\"Решение\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
            "</workbook>";

        private const string WorkbookRelsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";

        private const string StylesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<fonts count=\"6\">" +
                "<font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
                "<font><b/><sz val=\"14\"/><color rgb=\"FF1E3A8A\"/><name val=\"Calibri\"/></font>" +
                "<font><b/><sz val=\"11\"/><color rgb=\"FF1F2937\"/><name val=\"Calibri\"/></font>" +
                "<font><b/><sz val=\"11\"/><color rgb=\"FF1D4ED8\"/><name val=\"Calibri\"/></font>" +
                "<font><b/><sz val=\"12\"/><color rgb=\"FF92400E\"/><name val=\"Calibri\"/></font>" +
                "<font><i/><sz val=\"10\"/><color rgb=\"FF4B5563\"/><name val=\"Calibri\"/></font>" +
            "</fonts>" +
            "<fills count=\"6\">" +
                "<fill><patternFill patternType=\"none\"/></fill>" +
                "<fill><patternFill patternType=\"gray125\"/></fill>" +
                "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFDBEAFE\"/></patternFill></fill>" +
                "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF3F4F6\"/></patternFill></fill>" +
                "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFEFF6FF\"/></patternFill></fill>" +
                "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFEF3C7\"/></patternFill></fill>" +
            "</fills>" +
            "<borders count=\"2\">" +
                "<border><left/><right/><top/><bottom/><diagonal/></border>" +
                "<border>" +
                    "<left style=\"thin\"><color rgb=\"FFD1D5DB\"/></left>" +
                    "<right style=\"thin\"><color rgb=\"FFD1D5DB\"/></right>" +
                    "<top style=\"thin\"><color rgb=\"FFD1D5DB\"/></top>" +
                    "<bottom style=\"thin\"><color rgb=\"FFD1D5DB\"/></bottom>" +
                    "<diagonal/>" +
                "</border>" +
            "</borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"9\">" +
                "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
                "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
                "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
                "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
                "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
                "<xf numFmtId=\"0\" fontId=\"3\" fillId=\"4\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
                "<xf numFmtId=\"0\" fontId=\"4\" fillId=\"5\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
                "<xf numFmtId=\"0\" fontId=\"5\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
            "</cellXfs>" +
            "</styleSheet>";
    }
}
