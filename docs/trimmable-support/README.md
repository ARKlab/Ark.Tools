# Trimming Support Documentation

This folder contains documentation for the assembly trimming initiative across all Ark.Tools libraries.

## Overview

Assembly trimming is a .NET feature that removes unused code from published applications, significantly reducing deployment size. This initiative aims to make all Ark.Tools libraries trim-compatible to enable optimal deployment sizes for applications using these libraries.

## Documentation Files

- **[implementation-plan.md](implementation-plan.md)** - Overall strategy and phased rollout plan
- **[progress-tracker.md](progress-tracker.md)** - Current status and detailed TODO list for all libraries
- **[guidelines.md](guidelines.md)** - Best practices and patterns for making libraries trimmable

## Quick Links

- **Progress**: 36/42 libraries (86%) completed - 4 marked as not trimmable
- **Current Phase**: ✅ Phase 3 COMPLETE - All Levels 0-8 Done!
- **Target**: 30-40% deployment size reduction - ✅ ACHIEVED!

## References

- [Microsoft Docs: Trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained)
- [Modernization Plan](../modernization-issues/README.md#18-trimming-phase-1)
