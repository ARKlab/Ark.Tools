// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Microsoft.VisualStudio.TestTools.UnitTesting;

// Disable parallel test execution for BDD scenarios
[assembly: Parallelize(Scope = ExecutionScope.ClassLevel, Workers = 1)]
