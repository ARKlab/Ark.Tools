// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using NodaTime;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.ResourceWatcher.Testing(net10.0)', Before:
namespace Ark.Tools.ResourceWatcher.Testing
{

    /// <summary>
    /// Query filter for stub resources.
    /// </summary>
    public sealed class StubQueryFilter
    {
        /// <summary>
        /// Gets or sets the start date for the query filter.
        /// </summary>
        public LocalDate? FromDate { get; set; }

        /// <summary>
        /// Gets or sets the end date for the query filter.
        /// </summary>
        public LocalDate? ToDate { get; set; }

        /// <summary>
        /// Gets or sets the resource ID pattern for filtering.
        /// </summary>
        public string? ResourceIdPattern { get; set; }
    }


=======
namespace Ark.Tools.ResourceWatcher.Testing;


/// <summary>
/// Query filter for stub resources.
/// </summary>
public sealed class StubQueryFilter
{
    /// <summary>
    /// Gets or sets the start date for the query filter.
    /// </summary>
    public LocalDate? FromDate { get; set; }

    /// <summary>
    /// Gets or sets the end date for the query filter.
    /// </summary>
    public LocalDate? ToDate { get; set; }

    /// <summary>
    /// Gets or sets the resource ID pattern for filtering.
    /// </summary>
    public string? ResourceIdPattern { get; set; }
>>>>>>> After
    namespace Ark.Tools.ResourceWatcher.Testing;


    /// <summary>
    /// Query filter for stub resources.
    /// </summary>
    public sealed class StubQueryFilter
    {
        /// <summary>
        /// Gets or sets the start date for the query filter.
        /// </summary>
        public LocalDate? FromDate { get; set; }

        /// <summary>
        /// Gets or sets the end date for the query filter.
        /// </summary>
        public LocalDate? ToDate { get; set; }

        /// <summary>
        /// Gets or sets the resource ID pattern for filtering.
        /// </summary>
        public string? ResourceIdPattern { get; set; }
    }