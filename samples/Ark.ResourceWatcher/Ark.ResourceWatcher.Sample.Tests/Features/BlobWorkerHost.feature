@BlobWorkerHost
Feature: Blob Worker Host Integration
    As a developer
    I want to verify the BlobWorkerHost processes blobs correctly
    So that I can ensure reliable ETL operations

Background:
    Given a mock blob storage API is configured
    And a mock sink API is configured

@NewBlob
Scenario: New blob is fetched and processed
    Given a blob "data/file1.csv" exists with checksum "abc123" and content:
        """
        Id,Name,Value
        001,Widget,100.00
        002,Gadget,250.50
        """
    When the worker runs one cycle
    Then the blob "data/file1.csv" should be processed
    And the sink API should receive 2 records
    And the resource "data/file1.csv" state should be "Processed"
