using System;
using System.Globalization;
using System.IO;
using System.Text;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services
{
    /// <summary>
    /// Stores the user's scaffold catalogue choice outside the Revit document. This is
    /// a workstation preference, not model geometry, so an unreadable file must never
    /// prevent the add-in from opening or fall back to an empty selection.
    /// </summary>
    internal static class ScaffoldSystemPreferenceStore
    {
        private const string FileHeader = "DALTUDTXD-SCAFFOLD-SYSTEMS-V1|";

        private static string SettingsPath
        {
            get
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(root, "DALTUDTXD_68TH4_PMTKVK_REVITAPI", "scaffold-systems.config");
            }
        }

        public static bool TryLoad(out ScaffoldSystemType systems)
        {
            systems = ScaffoldSystemType.Ringlock;
            try
            {
                string path = SettingsPath;
                if (!File.Exists(path)) return false;
                string text = File.ReadAllText(path, Encoding.UTF8).Trim();
                if (!text.StartsWith(FileHeader, StringComparison.Ordinal)) return false;
                if (!int.TryParse(text.Substring(FileHeader.Length), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int raw)) return false;

                ScaffoldSystemType parsed = (ScaffoldSystemType)raw;
                ScaffoldSystemType supported = parsed & ScaffoldSystemTypeExtensions.SupportedSystems;
                if (supported == ScaffoldSystemType.None) return false;
                systems = supported;
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (NotSupportedException) { return false; }
            catch (ArgumentException) { return false; }
        }

        public static bool TrySave(ScaffoldSystemType systems, out string error)
        {
            error = null;
            try
            {
                systems = systems.Normalize();
                string path = SettingsPath;
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(path,
                    FileHeader + ((int)systems).ToString(CultureInfo.InvariantCulture), Encoding.UTF8);
                return true;
            }
            catch (IOException exception) { error = exception.Message; }
            catch (UnauthorizedAccessException exception) { error = exception.Message; }
            catch (NotSupportedException exception) { error = exception.Message; }
            catch (ArgumentException exception) { error = exception.Message; }
            return false;
        }
    }
}
