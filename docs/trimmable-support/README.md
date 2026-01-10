# Trimming Support Documentation

This folder contains documentation for the assembly trimming initiative across all Ark.Tools libraries.

## Overview

Assembly trimming is a .NET feature that removes unused code from published applications, significantly reducing deployment size. This initiative aims to make all Ark.Tools libraries trim-compatible to enable optimal deployment sizes for applications using these libraries.

## Documentation Files

- **[implementation-plan.md](implementation-plan.md)** - Overall strategy and phased rollout plan
- **[progress-tracker.md](progress-tracker.md)** - Current status and detailed TODO list for all libraries
- **[guidelines.md](guidelines.md)** - Best practices and patterns for making libraries trimmable

## Quick Links

- **Progress**: 7/42 libraries (17%) completed
- **Current Phase**: Phase 1 - Foundation Libraries
- **Major Milestone**: Level 0 Foundation Libraries 100% Complete! 🎉
- **Target**: 30-40% deployment size reduction

## References

- [Microsoft Docs: Trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained)
- [Modernization Plan](../modernization-issues/README.md#18-trimming-phase-1)
