// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.EntityFrameworkCore.Internal;

namespace Microsoft.EntityFrameworkCore.Tools.DotNet.Internal
{
    public class DotNetProjectBuilder : IProjectBuilder
    {
        public void EnsureBuild(IProjectContext project)
        {
            ICommand command;
            if (project is DotNetProjectContext)
            {
                command = CreateDotNetBuildCommand(project);
            }
            else if (project is MsBuildProjectContext)
            {
                command = CreateMsBuildBuildCommand(project);
            }
            else
            {
                throw new InvalidOperationException("Unrecognized project type");
            }

            var buildExitCode = command
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute()
                .ExitCode;

            if (buildExitCode != 0)
            {
                throw new OperationErrorException(ToolsDotNetStrings.BuildFailed(project.ProjectName));
            }
        }

        private static ICommand CreateMsBuildBuildCommand([NotNull] IProjectContext projectContext)
        {
            // TODO 'build3' is a placeholder name
            // TODO align with actual command line arguments once those settle
            return Command.CreateDotNet("build3", new[]
            {
                projectContext.ProjectFullPath,
                "/t:Build",
                // GenerateDependencyFile forces deps.json file to always be generated
                $"/p:Configuration={projectContext.Configuration};GenerateDependencyFile=true" 
            });
        }

        private static ICommand CreateDotNetBuildCommand([NotNull] IProjectContext projectContext)
        {
            var args = new List<string>
            {
                projectContext.ProjectFullPath,
                "--configuration", projectContext.Configuration,
                "--framework", projectContext.TargetFramework.GetShortFolderName()
            };

            if (projectContext.TargetDirectory != null)
            {
                args.Add("--output");
                args.Add(projectContext.TargetDirectory);
            }

            return Command.CreateDotNet(
                "build",
                args,
                projectContext.TargetFramework,
                projectContext.Configuration);
        }
    }
}
