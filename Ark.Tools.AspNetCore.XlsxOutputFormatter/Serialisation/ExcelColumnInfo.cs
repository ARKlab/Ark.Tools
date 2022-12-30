// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.AspNetCore.XlsxOutputFormatter.Attributes;

namespace Ark.Tools.AspNetCore.XlsxOutputFormatter.Serialisation
{
    /// <summary>
    /// Formatting information for an Excel column based on attribute values specified on a class.
    /// </summary>
    public class ExcelColumnInfo
    {
        public string PropertyName { get; set; }
        public ExcelColumnAttribute? ExcelAttribute { get; set; }
        public string? FormatString { get; set; }
        public string Header { get; set; }

        public string ExcelNumberFormat
        {
            get { return ExcelAttribute != null ? ExcelAttribute.NumberFormat : null; }
        }

        public bool IsExcelHeaderDefined
        {
            get { return ExcelAttribute != null && ExcelAttribute.Header != null; }
        }

        public ExcelColumnInfo(string propertyName, ExcelColumnAttribute? excelAttribute = null, string? formatString = null)
        {
            PropertyName = propertyName;
            ExcelAttribute = excelAttribute;
            FormatString = formatString;
            Header = IsExcelHeaderDefined ? ExcelAttribute.Header : propertyName;
        }
    }
}
