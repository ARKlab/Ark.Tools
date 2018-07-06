using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Ark.AspNetCore.XlsxOutputFormatter
{
    public class XlsxDocumentBuilder : IDisposable
    {
        public ExcelPackage Package { get; set; }
        public ExcelWorksheet Worksheet { get; set; }
        public int RowCount { get; set; }
        
        public XlsxDocumentBuilder()
        {
            // Create a worksheet
            Package = new ExcelPackage();
            Package.Workbook.Worksheets.Add("Data");
            Worksheet = Package.Workbook.Worksheets[1];

            RowCount = 0;
        }

        public void AutoFit()
        {
            Worksheet.Cells[Worksheet.Dimension.Address].AutoFitColumns();
        }

        public Task WriteToStream(Stream stream)
        {
            Package.SaveAs(stream);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Append a row to the XLSX worksheet.
        /// </summary>
        /// <param name="row">The row to append to this instance.</param>
        public void AppendRow(IEnumerable<object> row)
        {
            RowCount++;
            
            int i = 0;
            foreach (var col in row)
            {
                Worksheet.Cells[RowCount, ++i].Value = col;
            }
        }

        public void FormatColumn(int column, string format, bool skipHeaderRow = true)
        {
            var firstRow = skipHeaderRow ? 2 : 1;

            if (firstRow <= RowCount)
                Worksheet.Cells[firstRow, column, RowCount, column].Style.Numberformat.Format = format;
        }

        public static bool IsExcelSupportedType(object expression)
        {
            return expression is string
                || expression is short
                || expression is int
                || expression is long
                || expression is decimal
                || expression is float
                || expression is double
                || expression is DateTime;
        }

        public void Dispose()
        {
            Package.Dispose();
        }
    }
}
