﻿using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplicationDemo.Dto
{
    public class MarketRecordV0
    {
        public string? Market { get; set; }
        public DateTimeOffset DateTimeOffset { get; set; }
    }
}
