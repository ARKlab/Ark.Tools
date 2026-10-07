# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- `ArkAdaptiveSampler` has a constructor that accepts a `TimeProvider`, so tests can control sampling time.

### Changed

- `Ark.Tools.MediatorFramework.Messaging.Azure` requires `Azure.Storage.Blobs` 12.30.1 and `Azure.Storage.Queues` 12.28.1.

### Fixed

- `ArkAdaptiveSampler` measures elapsed time with a monotonic clock, so system clock changes no longer distort trace rate limiting.
