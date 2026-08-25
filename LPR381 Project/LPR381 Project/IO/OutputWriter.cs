using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace LPR381_Project.IO
{
    internal class OutputWriter
    {
        private readonly StringBuilder _buffer = new StringBuilder();

        public void AppendLine(string text = "") => _buffer.AppendLine(text);

        public void AppendHeader(string title)
        {
            _buffer.AppendLine();
            _buffer.AppendLine(title);
            _buffer.AppendLine(new string('-', title.Length));
        }

        /// Formats a table of doubles as tab-separated, 3-decimal rows.
        public void AppendTable(double[,] table)
        {
            int rows = table.GetLength(0);
            int cols = table.GetLength(1);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    _buffer.Append(Round3(table[r, c]).ToString("0.000", CultureInfo.InvariantCulture));
                    if (c < cols - 1) _buffer.Append('\t');
                }
                _buffer.AppendLine();
            }
        }

     
        public void AppendTable(double[,] table, string[] columnHeaders, string[] rowLabels)
        {
            int rows = table.GetLength(0);
            int cols = table.GetLength(1);

            _buffer.Append("\t"); 
            for (int c = 0; c < cols; c++)
            {
                _buffer.Append(columnHeaders[c]);
                if (c < cols - 1) _buffer.Append('\t');
            }
            _buffer.AppendLine();

            for (int r = 0; r < rows; r++)
            {
                _buffer.Append(rowLabels[r]).Append('\t');
                for (int c = 0; c < cols; c++)
                {
                    _buffer.Append(Round3(table[r, c]).ToString("0.000", CultureInfo.InvariantCulture));
                    if (c < cols - 1) _buffer.Append('\t');
                }
                _buffer.AppendLine();
            }
        }

        public static double Round3(double value) => System.Math.Round(value, 3, System.MidpointRounding.AwayFromZero);

        public void Save(string filePath) => File.WriteAllText(filePath, _buffer.ToString());
    }
}
