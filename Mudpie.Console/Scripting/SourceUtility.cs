namespace Mudpie.Console.Scripting
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;

    using Configuration;

    using JetBrains.Annotations;

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
