// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.ProjectModel;
using NuGet.Frameworks;

namespace Microsoft.EntityFrameworkCore.Tools.DotNet.Internal
{
    public class ProjectContextFactory
    {
        public IProjectContext Create(string filePath,
            NuGetFramework framework,
            string configuration,
            string outputDir)
        {
            configuration = configuration ?? Constants.DefaultConfiguration;

            if (filePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                return CreateMsBuildContext(filePath, framework, configuration, outputDir);
            }

            if (filePath.EndsWith("project.json", StringComparison.OrdinalIgnoreCase))
            {
                return CreateDotNetContext(filePath, framework, configuration, outputDir);
            }

            var attr = File.GetAttributes(filePath);
            if ((attr & FileAttributes.Directory) == 0)
            {
                throw new OperationErrorException($"Project file type {Path.GetExtension(filePath)} not supported");
            }

            var csproj = Directory.EnumerateFiles(filePath, "*.csproj", SearchOption.TopDirectoryOnly).ToList();
            if (csproj.Count == 0)
            {
                Reporter.Verbose.WriteLine("Could not find a csproj file");
            }
            if (csproj.Count > 1)
            {
                throw new OperationErrorException($"Multiple projects found in the directory '{filePath}'");
            }
            if (csproj.Count == 1)
            {
                return CreateMsBuildContext(csproj[0], framework, configuration, outputDir);
            }

            return CreateDotNetContext(filePath, framework, configuration, outputDir);
        }

        private IProjectContext CreateDotNetContext(string filePath, NuGetFramework framework, string configuration, string outputDir)
        {
            var project = SelectCompatibleFramework(
                framework,
                ProjectContext.CreateContextForEachFramework(filePath,
                    runtimeIdentifiers: RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers()));

            return new DotNetProjectContext(project,
                configuration,
                outputDir);
        }

        private IProjectContext CreateMsBuildContext(string filePath, NuGetFramework framework, string configuration, string outputDir)
        {
            return new MsBuildProjectContext(filePath, configuration);
        }

        private ProjectContext SelectCompatibleFramework([CanBeNull] NuGetFramework target, IEnumerable<ProjectContext> contexts)
        {
            return NuGetFrameworkUtility.GetNearest(contexts, target ?? FrameworkConstants.CommonFrameworks.NetCoreApp10, f => f.TargetFramework)
                   ?? contexts.First();
        }
    }
}
