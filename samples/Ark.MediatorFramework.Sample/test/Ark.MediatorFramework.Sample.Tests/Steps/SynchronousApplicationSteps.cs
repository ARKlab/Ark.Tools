// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Tests.Hooks;

using AwesomeAssertions;

using Reqnroll;

namespace Ark.MediatorFramework.Sample.Tests.Steps;

/// <summary>Defines direct-contract steps for synchronous application behavior.</summary>
[Binding]
public sealed class SynchronousApplicationSteps
{
    private readonly SampleTestContext _sampleContext;
    private ShapeDescription? _shape;
    private double _circleRadius;

    /// <summary>Initializes a new instance of the <see cref="SynchronousApplicationSteps"/> class.</summary>
    /// <param name="sampleContext">The scenario's direct application context.</param>
    public SynchronousApplicationSteps(SampleTestContext sampleContext)
    {
        _sampleContext = sampleContext;
    }

    /// <summary>Dispatches a circle through the polymorphic application contract.</summary>
    /// <param name="radius">The circle radius.</param>
    [When(@"I describe a circle with radius (.*)")]
    public async Task DescribeCircle(double radius)
    {
        _circleRadius = radius;
        _shape = await _context.DispatchRequestAsync<DescribeShapeRequest, ShapeDescription>(
            new DescribeShapeRequest
            {
                Shape = new Circle { Radius = radius },
            }).ConfigureAwait(false);
    }

    /// <summary>Asserts the concrete shape and nested polymorphic result.</summary>
    /// <param name="area">The expected shape area.</param>
    [Then(@"the shape description is a circle with area (.*)")]
    public void ShapeDescriptionIsCircle(double area)
    {
        _shape.Should().NotBeNull();
        _shape!.Shape.Should().BeOfType<Circle>().Which.Radius.Should().Be(_circleRadius);
        _shape.Metadata.FeaturedShape.Should().BeOfType<Circle>();
        _shape.Area.Should().BeApproximately(area, 0.000000000000001);
    }

    private ApplicationTestContext _context => _sampleContext.Application;
}
