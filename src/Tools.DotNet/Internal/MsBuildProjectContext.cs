// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using NuGet.Frameworks;

namespace Microsoft.EntityFrameworkCore.Tools.DotNet.Internal
{
    public class MsBuildProjectContext : IProjectContext
    {
        public MsBuildProjectContext(string filePath, string configuration)
        {
            var result = RunDesignTimeBuild(filePath, configuration);
            var project = result.ProjectStateAfterBuild;

            Configuration = configuration;
            ProjectName = Path.GetFileNameWithoutExtension(filePath);
            ProjectFullPath = GetProperty(project, "ProjectPath");
            RootNamespace = GetProperty(project, "RootNamespace") ?? ProjectName;
            TargetFramework = NuGetFramework.Parse(GetProperty(project, "NuGetTargetMoniker"));
            IsClassLibrary = GetProperty(project, "OutputType").Equals("Library", StringComparison.OrdinalIgnoreCase);
            TargetDirectory = GetProperty(project, "TargetDir");
            Platform = GetProperty(project, "Platform");
            AssemblyFullPath = GetProperty(project, "TargetPath");
            PackagesDirectory = GetProperty(project, "NuGetPackageRoot");

            // TODO get from actual properties according to TFM
            Config = AssemblyFullPath + ".config";
            RuntimeConfigJson = Path.Combine(TargetDirectory, Path.GetFileNameWithoutExtension(AssemblyFullPath), ".runtimeconfig.json");
            DepsJson = Path.Combine(TargetDirectory, Path.GetFileNameWithoutExtension(AssemblyFullPath), ".deps.json");
        }

        private BuildResult RunDesignTimeBuild(string filePath, string configuration)
        {
            // TODO get SDK from muxer
            string sdkPath = null; //@"C:\Users\namc\dev\dotnet-cli\artifacts\win10-x64\stage2\sdk\1.0.0-featmsbuild-003438";
            Debug.Assert(sdkPath != null, "For now, you need to manually add the SDK path for dotnet");
            var globalProperties = new Dictionary<string, string>
            {
                { "Configuration", configuration },
                { "GenerateDependencyFile", "true" },
                { "DesignTimeBuild", "true" },
                { "MSBuildExtensionsPath", sdkPath }
            };

            // TODO get this from .NET Core SDK
            Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", Path.Combine(sdkPath, "MSBuild.exe"));

            var xmlReader = XmlReader.Create(new FileStream(filePath, FileMode.Open));
            var projectCollection = new ProjectCollection();
            var xml = ProjectRootElement.Create(xmlReader, projectCollection);
            xml.FullPath = filePath;

            var project = new Project(xml, globalProperties, /*toolsVersion*/ null, projectCollection);

            var projectInstance = project.CreateProjectInstance();
            var buildRequest = new BuildRequestData(projectInstance, new[] { "Build" });
            var buildParams = new BuildParameters(project.ProjectCollection);

            var result = BuildManager.DefaultBuildManager.Build(buildParams, buildRequest);

            // this is a hack for failed project builds. ProjectStateAfterBuild == null after a failed build
            result.ProjectStateAfterBuild = projectInstance;

            return result;
        }

        private string GetProperty(ProjectInstance project, string propertyName)
            => project.Properties.FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))?.EvaluatedValue;

        public NuGetFramework TargetFramework { get; }
        public bool IsClassLibrary { get; }
        public string Config { get; }
        public string DepsJson { get; }
        public string RuntimeConfigJson { get; }
        public string PackagesDirectory { get; }
        public string AssemblyFullPath { get; }
        public string ProjectName { get; }
        public string Configuration { get; }
        public string Platform { get; }
        public string ProjectFullPath { get; }
        public string RootNamespace { get; }
        public string TargetDirectory { get; }
    }
}