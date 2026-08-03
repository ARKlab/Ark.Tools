// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Data;

namespace Ark.Tools.Core.Benchmarks;

internal static class RowsAddConverter
{
    internal static DataTable Convert(BenchmarkEntity[] source)
    {
        var table = new DataTable(nameof(BenchmarkEntity));
        table.Columns.Add(nameof(BenchmarkEntity.Id), typeof(int));
        table.Columns.Add(nameof(BenchmarkEntity.Name), typeof(string));
        table.Columns.Add(nameof(BenchmarkEntity.Measurement), typeof(double));
        table.Columns.Add(nameof(BenchmarkEntity.Amount), typeof(decimal));
        table.Columns.Add(nameof(BenchmarkEntity.IsEnabled), typeof(bool));
        table.Columns.Add(nameof(BenchmarkEntity.OptionalCount), typeof(int));
        table.Columns.Add(nameof(BenchmarkEntity.CreatedAt), typeof(DateTime));
        table.Columns.Add(nameof(BenchmarkEntity.Status), typeof(string));
        table.Columns.Add(nameof(BenchmarkEntity.CorrelationId), typeof(Guid));
        table.Columns.Add(nameof(BenchmarkEntity.EffectiveDate), typeof(DateTime));

        table.BeginLoadData();
        try
        {
            foreach (var item in source)
            {
                var row = table.NewRow();
                row.ItemArray =
                [
                    item.Id,
                    item.Name,
                    item.Measurement,
                    item.Amount,
                    item.IsEnabled,
                    item.OptionalCount,
                    item.CreatedAt,
                    item.Status.ToString(),
                    item.CorrelationId,
                    item.EffectiveDate.ToDateTimeUnspecified(),
                ];
                table.Rows.Add(row);
            }
        }
        finally
        {
            table.EndLoadData();
        }

        return table;
    }
}
