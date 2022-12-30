// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;

namespace Ark.Tools.AspNetCore.XlsxOutputFormatter.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ExcelDocumentAttribute : Attribute
    {
        
        /// <summary>
        /// Set properties of Excel documents generated from this type.
        /// </summary>
        public ExcelDocumentAttribute() : this(string.Empty) { }

        /// <summary>
        /// Set properties of Excel documents generated from this type.
        /// </summary>
        /// <param name="fileName">The preferred file name for an Excel document generated from this type.</param>
        public ExcelDocumentAttribute(string fileName)
        {
            FileName = fileName;
        }

        /// <summary>
        /// The preferred file name for an Excel document generated from this type.
        /// </summary>
        public string FileName { get; set; }
    }
}
