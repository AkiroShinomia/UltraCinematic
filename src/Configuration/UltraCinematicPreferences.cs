using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;

namespace UltraCinematic.Configuration
{
    internal enum InterfaceLanguage
    {
        English,
        Russian
    }

    internal enum InterfaceStyle
    {
        Classic,
        Dark
    }

    internal sealed class UltraCinematicPreferences
    {
        private readonly ConfigFile config;
        private readonly ConfigEntry<InterfaceLanguage> language;
        private readonly ConfigEntry<InterfaceStyle> style;
        private readonly ConfigEntry<string> timelineDirectory;

        internal UltraCinematicPreferences(ConfigFile configFile)
        {
            config = configFile;
            language = config.Bind(
                "Interface",
                "Language",
                InterfaceLanguage.English,
                "Language used by UltraCinematic UI: English or Russian.");
            style = config.Bind(
                "Interface",
                "Style",
                InterfaceStyle.Classic,
                "Timeline UI style: Classic or Dark.");
            timelineDirectory = config.Bind(
                "Storage",
                "TimelineDirectory",
                DefaultTimelineDirectory,
                "Absolute directory used for level-specific Timeline project saves.");

            string error;
            string normalized;
            if (!TryNormalizeDirectory(timelineDirectory.Value, out normalized, out error))
            {
                timelineDirectory.Value = DefaultTimelineDirectory;
                Directory.CreateDirectory(DefaultTimelineDirectory);
                config.Save();
            }
            else if (!string.Equals(timelineDirectory.Value, normalized, StringComparison.Ordinal))
            {
                timelineDirectory.Value = normalized;
                config.Save();
            }
        }

        internal InterfaceLanguage Language
        {
            get => language.Value;
            set
            {
                if (language.Value == value) return;
                language.Value = value;
                config.Save();
            }
        }

        internal InterfaceStyle Style
        {
            get => style.Value;
            set
            {
                if (style.Value == value) return;
                style.Value = value;
                config.Save();
            }
        }

        internal string TimelineDirectory => timelineDirectory.Value;
        internal static string DefaultTimelineDirectory => Path.Combine(Paths.ConfigPath, "UltraCinematic", "Timelines");

        internal bool TrySetTimelineDirectory(string value, out string error)
        {
            string normalized;
            if (!TryNormalizeDirectory(value, out normalized, out error)) return false;
            timelineDirectory.Value = normalized;
            config.Save();
            return true;
        }

        internal bool TryResetTimelineDirectory(out string error)
        {
            return TrySetTimelineDirectory(DefaultTimelineDirectory, out error);
        }

        private static bool TryNormalizeDirectory(string value, out string normalized, out string error)
        {
            normalized = "";
            error = "";
            try
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    error = UiText.T("The save folder cannot be empty.", "Папка сохранений не может быть пустой.");
                    return false;
                }

                string expanded = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
                if (!Path.IsPathRooted(expanded))
                {
                    error = UiText.T("Enter an absolute folder path.", "Укажите полный путь к папке.");
                    return false;
                }

                normalized = Path.GetFullPath(expanded).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(normalized);
                if (!Directory.Exists(normalized))
                {
                    error = UiText.T("The selected folder could not be created.", "Не удалось создать выбранную папку.");
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                error = UiText.T("Could not use this folder: ", "Не удалось использовать эту папку: ") + exception.Message;
                return false;
            }
        }
    }

    internal static class UiText
    {
        internal static bool IsRussian => UltraCinematicPlugin.Preferences != null &&
                                           UltraCinematicPlugin.Preferences.Language == InterfaceLanguage.Russian;

        internal static string T(string english, string russian)
        {
            return IsRussian ? russian : english;
        }

        internal static string F(string english, string russian, params object[] arguments)
        {
            return string.Format(T(english, russian), arguments);
        }
    }
}
