// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.ResourceWatcher.Sample.Dto;
using Ark.ResourceWatcher.Sample.Transform;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.ResourceWatcher.Sample.Tests.Transform;

/// <summary>
/// Data-driven unit tests for the CsvTransformService.
/// Tests are discovered automatically from files in TestData/Transform folder.
/// </summary>
/// <remarks>
/// To add a new test case:
/// 1. Create a file named {caseName}_input.csv with the input CSV content
/// 2. Create either:
///    - {caseName}_output.json for expected successful output
///    - {caseName}_error.json for expected error cases
/// 
/// The test will automatically pick up new files without code changes.
/// </remarks>
[TestClass]
public sealed class MyTransformServiceTests
{
    private const string _testSourceId = "test-source";

    private static readonly string _testDataPath = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "TestData",
        "Transform");

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    /// <summary>
    /// Discovers all test cases by finding *_input.* files in TestData/Transform.
    /// Returns test data for success cases (with _output.json) and error cases (with _error.json).
    /// </summary>
    public static IEnumerable<object[]> GetTransformTestCases()
    {
        if (!Directory.Exists(_testDataPath))
        {
            yield break;
        }

        var inputFiles = Directory.GetFiles(_testDataPath, "*_input.*");
        foreach (var inputFile in inputFiles)
        {
            var fileName = Path.GetFileName(inputFile);
            var extension = Path.GetExtension(inputFile);
            var caseName = fileName.Replace("_input" + extension, "", StringComparison.OrdinalIgnoreCase);

            var outputFile = Path.Combine(_testDataPath, $"{caseName}_output.json");
            var errorFile = Path.Combine(_testDataPath, $"{caseName}_error.json");

            var isErrorCase = File.Exists(errorFile);
            var expectedFile = isErrorCase ? errorFile : outputFile;

            if (File.Exists(expectedFile))
            {
                yield return [caseName, inputFile, expectedFile, isErrorCase];
            }
        }
    }

    /// <summary>
    /// Gets display name for the test case in test explorer.
    /// </summary>
    public static string GetTestDisplayName(MethodInfo _, object[] data)
    {
        var caseName = (string)data[0];
        var isErrorCase = (bool)data[3];
        return isErrorCase ? $"Transform_Error_{caseName}" : $"Transform_Success_{caseName}";
    }

    /// <summary>
    /// Data-driven test that validates transformation logic against file-based test data.
    /// </summary>
    /// <param name="caseName">The name of the test case (derived from filename).</param>
    /// <param name="inputPath">Path to the input file.</param>
    /// <param name="expectedPath">Path to the expected output or error file.</param>
    /// <param name="isErrorCase">True if this is an error test case.</param>
    [TestMethod]
    [DynamicData(nameof(GetTransformTestCases), DynamicDataDisplayName = nameof(GetTestDisplayName))]
    public void Transform_DataDrivenCase(string caseName, string inputPath, string expectedPath, bool isErrorCase)
    {
        // Arrange
        var service = new MyTransformService(_testSourceId);
        var input = File.ReadAllBytes(inputPath);
        var expectedJson = File.ReadAllText(expectedPath);

        if (isErrorCase)
        {
            // Act & Assert - Error case
            var expectedError = JsonSerializer.Deserialize<ExpectedError>(expectedJson, _jsonOptions);
            expectedError.Should().NotBeNull($"Failed to deserialize error expectation for case '{caseName}'");

            var act = () => service.Transform(input);

            var ex = act.Should().Throw<Exception>().Which;
            ex.GetType().Name.Should().Be(expectedError!.ExceptionType,
                $"Expected exception type '{expectedError.ExceptionType}' for case '{caseName}'");
            ex.Message.Should().Contain(expectedError.MessageContains,
                $"Expected message to contain '{expectedError.MessageContains}' for case '{caseName}'");
        }
        else
        {
            // Act
            var result = service.Transform(input);

            // Assert - Compare JSON serialization for flexible comparison
            var expected = JsonSerializer.Deserialize<SinkDto>(expectedJson, _jsonOptions);
            expected.Should().NotBeNull($"Failed to deserialize expected output for case '{caseName}'");

            result.Should().BeEquivalentTo(expected, options => options
                .ComparingByMembers<SinkDto>()
                .ComparingByMembers<SinkRecord>(),
                because: $"Full object equivalence for case '{caseName}'");
        }
    }

    /// <summary>
    /// Tests that null input throws ArgumentNullException.
    /// </summary>
    [TestMethod]
    public void Transform_NullInput_ThrowsArgumentNullException()
    {
        // Arrange
        var service = new MyTransformService(_testSourceId);

        // Act
        var act = () => service.Transform(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("input");
    }

    /// <summary>
    /// Tests that different source IDs are preserved in output.
    /// </summary>
    [TestMethod]
    public void Transform_DifferentSourceId_PreservedInOutput()
    {
        // Arrange
        var customSourceId = "custom-blob-id-12345";
        var service = new MyTransformService(customSourceId);
        var input = "id,name,value\n1,Test,10.0"u8.ToArray();

        // Act
        var result = service.Transform(input);

        // Assert
        result.SourceId.Should().Be(customSourceId);
    }
}

/// <summary>
/// Schema for *_error.json files.
/// </summary>
/// <param name="ExceptionType">The expected exception type name (e.g., "TransformException").</param>
/// <param name="MessageContains">A substring that should be contained in the exception message.</param>
public sealed record ExpectedError(string ExceptionType, string MessageContains);