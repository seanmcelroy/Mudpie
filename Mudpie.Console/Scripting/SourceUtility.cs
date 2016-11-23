// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SourceUtility.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   Utility classes for reading script source files from disk, local storage, and memory
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace Mudpie.Console.Scripting
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;

    using Configuration;

    using JetBrains.Annotations;

    /// <summary>
    /// Utility classes for reading script source files from disk, local storage, and memory
    /// </summary>
    internal static class SourceUtility
    {
        [NotNull, ItemCanBeNull, Pure]
        public static async Task<string> GetSourceCodeLinesAsync([NotNull] MudpieConfigurationSection configSection, [NotNull] string programFileName)
        {
            if (configSection == null)
            {
                throw new ArgumentNullException(nameof(configSection));
            }

            if (string.IsNullOrWhiteSpace(programFileName))
            {
                throw new ArgumentNullException(nameof(programFileName));
            }

            Debug.Assert(configSection != null, "configSection != null");
            Debug.Assert(configSection.Directories != null, "configSection.Directories != null");
            foreach (var dir in configSection.Directories)
            {
                if (dir != null)
                {
                    foreach (var file in Directory.GetFiles(dir.Directory))
                    {
                        Debug.Assert(file != null, "file != null");
                        if (string.Compare(
                                Path.GetFileNameWithoutExtension(file),
                                Path.GetFileNameWithoutExtension(programFileName),
                                StringComparison.OrdinalIgnoreCase) != 0)
                        {
                            continue;
                        }

                        using (var sr = new StreamReader(file))
                        {
                            // ReSharper disable once PossibleNullReferenceException
                            var contents = await sr.ReadToEndAsync();
                            sr.Close();
                            return contents;
                        }
                    }
                }
            }

            return null;
        }
    }
}
