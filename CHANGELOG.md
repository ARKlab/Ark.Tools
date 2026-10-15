# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Updated dependencies to their latest minor and patch versions (Microsoft.Extensions/AspNetCore 10.0.13, Swashbuckle 10.3.0, NLog 6.2.2, Rebus 8.9.5, Microsoft.Data.SqlClient 7.1.1 and others) and the .NET SDK to 10.0.402.

### Security

- Pinned the transitive `System.Security.Cryptography.Pkcs` dependency to 10.0.13 to address GHSA-846f-f764-76mx and GHSA-2fmv-rv2c-59x7.
