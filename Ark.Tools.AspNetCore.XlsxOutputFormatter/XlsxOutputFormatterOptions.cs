// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using OfficeOpenXml.Style;
using System;

namespace Ark.Tools.AspNetCore.XlsxOutputFormatter
{
    public class XlsxOutputFormatterOptions
    {
        /// <summary>
        /// An action method that can be used to set the default cell style.
        /// </summary>
        public Action<ExcelStyle>? CellStyle { get; set; }

        /// <summary>
        /// An action method that can be used to set the default header row style.
        /// </summary>
        public Action<ExcelStyle>? HeaderStyle { get; set; }

        /// <summary>
        /// True if columns should be auto-fit to the cell contents after writing.
        /// </summary>
        public bool AutoFit { get; set; }

        /// <summary>
        /// True if an auto-filter should be enabled for the data.
        /// </summary>
        public bool AutoFilter { get; set; }

        /// <summary>
        /// True if the header row should be frozen.
        /// </summary>
        public bool FreezeHeader { get; set; }

        /// <summary>
        /// Height for header row. (Default if null.)
        /// </summary>
        public double? HeaderHeight { get; set; }
    }
}