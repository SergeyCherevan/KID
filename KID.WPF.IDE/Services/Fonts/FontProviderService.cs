using KID.Resources;
using KID.Services.Fonts.Interfaces;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Windows.Media;

namespace KID.Services.Fonts
{
    /// <summary>
    /// Реализация сервиса предоставления списка шрифтов и размеров.
    /// </summary>
    public class FontProviderService : IFontProviderService
    {
        private static readonly ResourceManager _resourceManager =
            new ResourceManager("KID.Resources.AvailableFontsAndSizes", typeof(Strings).Assembly);

        /// <inheritdoc />
        public IEnumerable<string> GetAvailableFonts()
        {
            var result = new List<string>();

            try
            {
                var countStr = _resourceManager.GetString("Font_Count", CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(countStr) || !int.TryParse(countStr, out int count))
                    return GetDefaultFonts();

                var installedFontNames = System.Windows.Media.Fonts.SystemFontFamilies
                    .Select(f => f.Source)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < count; i++)
                {
                    var fontName = _resourceManager.GetString($"Font_{i}", CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(fontName) && installedFontNames.Contains(fontName) && !added.Contains(fontName))
                    {
                        added.Add(fontName);
                        result.Add(fontName);
                    }
                }

                if (result.Count == 0)
                    result.Add("Consolas");
            }
            catch
            {
                return GetDefaultFonts();
            }

            return result;
        }

        /// <inheritdoc />
        public IEnumerable<double> GetAvailableFontSizes()
        {
            var result = new List<double>();

            try
            {
                var countStr = _resourceManager.GetString("FontSize_Count", CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(countStr) || !int.TryParse(countStr, out int count))
                    return GetDefaultFontSizes();

                for (int i = 0; i < count; i++)
                {
                    var sizeStr = _resourceManager.GetString($"FontSize_{i}", CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(sizeStr) && double.TryParse(sizeStr, CultureInfo.InvariantCulture, out double size) && size > 0)
                        result.Add(size);
                }
            }
            catch
            {
                return GetDefaultFontSizes();
            }

            return result.Count > 0 ? result : GetDefaultFontSizes();
        }

        private static IEnumerable<string> GetDefaultFonts() => ["Consolas"];

        private static IEnumerable<double> GetDefaultFontSizes() =>
            [10.0, 11.0, 12.0, 13.0, 14.0, 16.0, 18.0, 20.0];
    }
}
