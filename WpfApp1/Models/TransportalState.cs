using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1.Models
{
    internal static class TransportState
    {
        public static int Rows { get; set; } = 3;
        public static int Cols { get; set; } = 5;

        public static double[,] Costs { get; set; } =
        {
            { 22, 14, 16, 28, 30 },
            { 19, 17, 26, 36, 36 },
            { 37, 30, 31, 39, 41 }
        };

        public static double[] Supply { get; set; } = { 350, 200, 300 };
        public static double[] Demand { get; set; } = { 170, 140, 200, 195, 145 };
    }
}
