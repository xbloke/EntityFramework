// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using NuGet.Frameworks;

namespace Microsoft.EntityFrameworkCore.Tools.DotNet.Internal
{
    public class MsBuildProjectContext : IProjectContext
    {
        private readonly Project _project;

        public MsBuildProjectContext(string filePath, string configuration)
        {
            _project = CreateProject(filePath, configuration);
            var result = RunDesignTimeBuild(_project);
            var projectInstance = result.ProjectStateAfterBuild;

            Configuration = configuration;
            ProjectName = Path.GetFileNameWithoutExtension(filePath);
            ProjectFullPath = FindProperty(projectInstance, "ProjectPath");
            RootNamespace = FindProperty(projectInstance, "RootNamespace") ?? ProjectName;
            TargetFramework = NuGetFramework.Parse(FindProperty(projectInstance, "NuGetTargetMoniker"));
            IsClassLibrary = FindProperty(projectInstance, "OutputType").Equals("Library", StringComparison.OrdinalIgnoreCase);
            TargetDirectory = FindProperty(projectInstance, "TargetDir");
            Platform = FindProperty(projectInstance, "Platform");
            AssemblyFullPath = FindProperty(projectInstance, "TargetPath");
            PackagesDirectory = FindProperty(projectInstance, "NuGetPackageRoot");

            // TODO get from actual properties according to TFM
            Config = AssemblyFullPath + ".config";
            RuntimeConfigJson = Path.Combine(TargetDirectory, Path.GetFileNameWithoutExtension(AssemblyFullPath), ".runtimeconfig.json");
            DepsJson = Path.Combine(TargetDirectory, Path.GetFileNameWithoutExtension(AssemblyFullPath), ".deps.json");
        }

        private BuildResult RunDesignTimeBuild(Project project)
        {
            var projectInstance = project.CreateProjectInstance();
            var buildRequest = new BuildRequestData(projectInstance, new[] { "Build" });
            var buildParams = new BuildParameters(project.ProjectCollection);

            var result = BuildManager.DefaultBuildManager.Build(buildParams, buildRequest);

            // this is a hack for failed project builds. ProjectStateAfterBuild == null after a failed build
            // But the properties are still available to be read
            result.ProjectStateAfterBuild = projectInstance;

            return result;
        }

        private static Project CreateProject(string filePath, string configuration)
        {
            var sdkPath = new DotNetSdkResolver().ResolveLatest();
            var msBuildFile = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "MSBuild.exe"
                : "MSBuild";

            Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", Path.Combine(sdkPath, msBuildFile));

            var globalProperties = new Dictionary<string, string>
            {
                { "Configuration", configuration },
                { "GenerateDependencyFile", "true" },
                { "DesignTimeBuild", "true" },
                { "MSBuildExtensionsPath", sdkPath }
            };

            var xmlReader = XmlReader.Create(new FileStream(filePath, FileMode.Open));
            var projectCollection = new ProjectCollection();
            var xml = ProjectRootElement.Create(xmlReader, projectCollection);
            xml.FullPath = filePath;

            var project = new Project(xml, globalProperties, /*toolsVersion*/ null, projectCollection);
            return project;
        }

        private string FindProperty(ProjectInstance project, string propertyName)
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
        public IProjectFile ProjectFile => new MsBuildProjectFile(_project);
    }
}