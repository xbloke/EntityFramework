// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.EntityFrameworkCore.Tools.DotNet.Internal
{
    public class MsBuildProjectFile : IProjectFile
    {
        private readonly Project _project;

        public MsBuildProjectFile(Project project)
        {
            _project = project;
        }

        public void AddDocument(string filePath)
        {
            var relativePath = GetProjectRelativePath(filePath);
            // TODO check if globbing makes adding this unnecessary
            Dictionary<string, string> metadata = null;
            if (relativePath.StartsWith(".."))
            {
                metadata = new Dictionary<string, string>()
                {
                    // currently this only adds items to the top-level folder in the project. This matches the behavior
                    // how the call to $dteProject.ProjectItems.AddFromFile(path) handles files outside the project dir
                    { "link", Path.GetFileName(filePath) }
                };
            }
            _project.AddItem("Compile", relativePath, metadata);
        }

        private string GetProjectRelativePath(string filePath)
            => PathUtility.GetRelativePath(_project.DirectoryPath, filePath);

        public void RemoveDocument(string filePath)
        {
            var items = _project.GetItems("Compile");
            var relativePath = GetProjectRelativePath(filePath);
            var item = items.FirstOrDefault(f => PathsEqual(f.EvaluatedInclude, filePath) || PathsEqual(f.EvaluatedInclude, relativePath)); 
            if (item != null)
            {
                _project.RemoveItem(item);
            }
        }

        public void Save()=> _project.Save();

        private static bool PathsEqual(string left, string right)
            => left.Equals(right, StringComparison.OrdinalIgnoreCase);
    }
}
