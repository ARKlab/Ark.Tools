// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Activity;

using AwesomeAssertions;

namespace Ark.Tools.Core.Tests;

[TestClass]
public sealed class ValueObjectAndResourceRegressionTests
{
    [TestMethod]
    public void Resource_DefaultInstanceHashCode_MatchesEquivalentNullInstance()
    {
        var left = default(Resource);
        var right = new Resource(null!, null!);

        left.Equals(right).Should().BeTrue();
        left.GetHashCode().Should().Be(right.GetHashCode());
    }

    [TestMethod]
    public void ValueObject_Equals_ReturnsFalseWhenAtomicValueSequencesHaveDifferentLengths()
    {
        var shorter = new TestValueObject(1);
        var longer = new TestValueObject(1, 2);

        shorter.Equals(longer).Should().BeFalse();
        longer.Equals(shorter).Should().BeFalse();
    }

    private sealed class TestValueObject(params int[] values) : ValueObject<TestValueObject>
    {
        protected override IEnumerable<object> GetAtomicValues()
        {
            return values.Cast<object>();
        }
    }
}
