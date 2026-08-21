using System;
using System.Collections.Generic;

using LibReFrontier.Abstractions;

namespace LibReFrontier.CLI
{
    /// <summary>
    /// Wording shared by every tool that moved from flat flags to verb commands.
    /// <para>ReFrontier, FrontierTextTool and FrontierDataTool all kept their old flags
    /// working while adding verbs. Keeping the deprecation notice and the verb/path
    /// ambiguity notice here means the three of them say the same thing.</para>
    /// </summary>
    public static class CliDeprecation
    {
        /// <summary>
        /// Report that a mode-selecting flag is deprecated and name the verb replacing it.
        /// </summary>
        /// <param name="flag">The deprecated flag, spelled as the user would type it.</param>
        /// <param name="replacement">The full command line to use instead.</param>
        public static void WarnFlag(string flag, string replacement)
        {
            Console.Error.WriteLine($"Warning: {flag} is deprecated and will be removed in a future release.");
            Console.Error.WriteLine($"  Use: {replacement}");
        }

        /// <summary>
        /// A verb name wins over a path that happens to be spelled the same way. When the
        /// only argument is such a path, say so rather than let the missing-argument error
        /// stand on its own.
        /// </summary>
        /// <param name="exampleCommand">Tool and verb to show in the suggested command line.</param>
        /// <param name="verbNames">Every verb the tool accepts.</param>
        /// <param name="args">Raw command line arguments.</param>
        /// <param name="fileSystem">File system used to check whether the path exists.</param>
        /// <returns>A message to show, or null when there is no ambiguity.</returns>
        public static string? DescribeVerbPathCollision(
            string exampleCommand,
            IReadOnlyList<string> verbNames,
            string[] args,
            IFileSystem fileSystem)
        {
            ArgumentNullException.ThrowIfNull(verbNames);
            ArgumentNullException.ThrowIfNull(args);
            ArgumentNullException.ThrowIfNull(fileSystem);

            if (args.Length != 1)
                return null;

            string candidate = args[0];
            bool isVerb = false;
            for (int i = 0; i < verbNames.Count; i++)
            {
                if (string.Equals(verbNames[i], candidate, StringComparison.Ordinal))
                {
                    isVerb = true;
                    break;
                }
            }
            if (!isVerb)
                return null;
            if (!fileSystem.FileExists(candidate) && !fileSystem.DirectoryExists(candidate))
                return null;

            return $"Note: '{candidate}' is both a command and a path that exists here; the command wins.\n"
                 + $"  To act on the path, name it explicitly: {exampleCommand} .{System.IO.Path.DirectorySeparatorChar}{candidate}";
        }
    }
}
