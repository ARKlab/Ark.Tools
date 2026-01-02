# Test Data for CsvTransformService

This folder contains data-driven test cases for the `CsvTransformService` transform logic.

## How It Works

Tests are automatically discovered from files in this folder. No code changes are needed to add new test cases.

## File Naming Convention

| Pattern | Description |
|---------|-------------|
| `{caseName}_input.csv` | Input CSV file |
| `{caseName}_output.json` | Expected output for success cases |
| `{caseName}_error.json` | Expected error details for failure cases |

## Adding a New Test Case

### Success Case

1. Create `my_case_input.csv` with the CSV content
2. Create `my_case_output.json` with the expected `SinkDto` result

### Error Case

1. Create `my_case_input.csv` with the invalid CSV content
2. Create `my_case_error.json` with the expected exception:

```json
{
  "exceptionType": "TransformException",
  "messageContains": "expected error message substring"
}
```

## Output JSON Schema

```json
{
  "sourceId": "test-source",
  "records": [
    {
      "id": "string",
      "name": "string",
      "value": 123.45,
      "properties": {
        "key": "value"
      }
    }
  ]
}
```

## Error JSON Schema

```json
{
  "exceptionType": "TransformException|FormatException|...",
  "messageContains": "substring to match in error message"
}
```

## Current Test Cases

| Case Name | Type | Description |
|-----------|------|-------------|
| `valid_single_record` | Success | Single record with basic fields |
| `valid_multiple_records` | Success | Multiple records with extra properties |
| `empty_file` | Success | Empty input returns empty records |
| `header_only` | Success | Header without data returns empty records |
| `decimal_precision` | Success | Decimal values with high precision |
| `whitespace_handling` | Success | Whitespace trimming in values |
| `malformed_csv_missing_columns` | Error | Less than 3 columns throws TransformException |
| `malformed_csv_invalid_decimal` | Error | Non-numeric value throws FormatException |

## Benefits

1. **Easy to add cases**: Just drop new files, no code changes
2. **Self-documenting**: File names describe the scenario
3. **Real-world debugging**: Copy production data as a new test case
4. **Version controlled**: Test data is tracked in git alongside code
