using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using CodeBuddy.Core.Interfaces;
using CodeBuddy.Core.Models.Documentation;

namespace CodeBuddy.Core.Implementation.Documentation
{
    /// <summary>
    /// Manages documentation versioning and history
    /// </summary>
    public class DocumentationVersionManager
    {
        private readonly IFileOperations _fileOps;
        private const string VersionHistoryPath = "docs/versions";

        public DocumentationVersionManager(IFileOperations fileOps)
        {
            _fileOps = fileOps;
        }

        /// <summary>
        /// Creates a new version of documentation
        /// </summary>
        public async Task<DocumentationVersion> CreateVersionAsync(string version, string description)
        {
            var docVersion = new DocumentationVersion
            {
                Version = version,
                Description = description,
                CreatedAt = DateTime.UtcNow,
                Files = new List<string>()
            };

            // Archive current documentation
            var currentFiles = await _fileOps.GetFilesAsync("docs");
            var versionPath = $"{VersionHistoryPath}/{version}";
            
            foreach (var file in currentFiles)
            {
                if (!file.Contains("versions"))
                {
                    var destPath = file.Replace("docs", versionPath);
                    await _fileOps.CopyFileAsync(file, destPath);
                    docVersion.Files.Add(file);
                }
            }

            // Save version metadata
            await _fileOps.WriteFileAsync(
                $"{versionPath}/version.json",
                System.Text.Json.JsonSerializer.Serialize(docVersion, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            return docVersion;
        }

        /// <summary>
        /// Gets a list of all documentation versions
        /// </summary>
        public async Task<List<DocumentationVersion>> GetVersionsAsync()
        {
            var versions = new List<DocumentationVersion>();
            var versionDirs = await _fileOps.GetDirectoriesAsync(VersionHistoryPath);

            foreach (var dir in versionDirs)
            {
                var metadataPath = $"{dir}/version.json";
                if (await _fileOps.FileExistsAsync(metadataPath))
                {
                    var metadata = await _fileOps.ReadFileAsync(metadataPath);
                    var version = System.Text.Json.JsonSerializer.Deserialize<DocumentationVersion>(metadata);
                    versions.Add(version);
                }
            }

            return versions.OrderByDescending(v => v.CreatedAt).ToList();
        }

        /// <summary>
        /// Gets documentation for a specific version
        /// </summary>
        public async Task<DocumentationArchive> GetVersionAsync(string version)
        {
            var versionPath = $"{VersionHistoryPath}/{version}";
            if (!await _fileOps.DirectoryExistsAsync(versionPath))
            {
                throw new DocumentationException($"Version {version} not found");
            }

            var metadataPath = $"{versionPath}/version.json";
            var metadata = await _fileOps.ReadFileAsync(metadataPath);
            var versionInfo = System.Text.Json.JsonSerializer.Deserialize<DocumentationVersion>(metadata);

            var archive = new DocumentationArchive
            {
                Version = versionInfo,
                Files = new Dictionary<string, string>()
            };

            foreach (var file in versionInfo.Files)
            {
                var archivePath = file.Replace("docs", versionPath);
                var content = await _fileOps.ReadFileAsync(archivePath);
                archive.Files[file] = content;
            }

            return archive;
        }

        /// <summary>
        /// Gets changes between two versions
        /// </summary>
        public async Task<DocumentationChanges> GetChangesAsync(string fromVersion, string toVersion)
        {
            var from = await GetVersionAsync(fromVersion);
            var to = await GetVersionAsync(toVersion);

            var changes = new DocumentationChanges
            {
                FromVersion = fromVersion,
                ToVersion = toVersion,
                ChangedFiles = new List<FileChange>()
            };

            foreach (var file in to.Version.Files)
            {
                if (!from.Files.ContainsKey(file))
                {
                    changes.ChangedFiles.Add(new FileChange 
                    { 
                        File = file, 
                        ChangeType = ChangeType.Added,
                        Content = to.Files[file]
                    });
                }
                else if (from.Files[file] != to.Files[file])
                {
                    changes.ChangedFiles.Add(new FileChange
                    {
                        File = file,
                        ChangeType = ChangeType.Modified,
                        Content = to.Files[file]
                    });
                }
            }

            foreach (var file in from.Version.Files)
            {
                if (!to.Files.ContainsKey(file))
                {
                    changes.ChangedFiles.Add(new FileChange
                    {
                        File = file,
                        ChangeType = ChangeType.Deleted,
                        Content = null
                    });
                }
            }

            return changes;
        }
    }
}