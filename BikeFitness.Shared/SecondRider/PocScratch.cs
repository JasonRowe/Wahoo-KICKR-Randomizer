using System;
using System.IO;

namespace BikeFitness.Shared.SecondRider
{
    /// <summary>
    /// The one place that owns the harness POC scratch directory.
    /// <para>
    /// POC code must never write to <c>AppSettings</c>, the app data folder, or a saved
    /// <c>Workout_*.json</c> / FIT file. Everything a POC persists goes under the OS temp directory so
    /// that uninstalling a POC is "delete one folder", with no user data at risk.
    /// </para>
    /// </summary>
    public static class PocScratch
    {
        /// <summary>Folder created under the OS temp directory for all harness POC state.</summary>
        public const string FolderName = "BikeFitnessPoc";

        /// <summary>Scratch directory path (not created until <see cref="EnsureDirectory"/>).</summary>
        public static string DirectoryPath => Path.Combine(Path.GetTempPath(), FolderName);

        /// <summary>Creates the scratch directory if needed and returns its path.</summary>
        public static string EnsureDirectory()
        {
            string dir = DirectoryPath;
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>Full path of a file inside the scratch directory.</summary>
        public static string FilePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("A file name is required.", nameof(fileName));
            }

            return Path.Combine(DirectoryPath, fileName);
        }

        /// <summary>
        /// True when <paramref name="path"/> is inside the scratch directory. Used to keep POC writes
        /// away from anything the user owns.
        /// </summary>
        public static bool IsInScratch(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            string root;
            string candidate;
            try
            {
                root = Path.GetFullPath(DirectoryPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                candidate = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                return false;
            }

            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return candidate.StartsWith(root, comparison);
        }
    }
}
