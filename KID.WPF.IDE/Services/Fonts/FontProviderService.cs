using KID.Models;
using KID.Services.Fonts.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace KID.Services.Fonts
{
    /// <summary>
    /// Реализация сервиса предоставления списка шрифтов и размеров.
    /// </summary>
    public class FontProviderService : IFontProviderService
    {
        /// <summary>
        /// Известные моноширинные шрифты в порядке приоритета отображения.
        /// </summary>
        private static readonly string[] KnownMonospaceFonts =
        {
            "Consolas",
            "Cascadia Code",
            "Cascadia Mono",
            "JetBrains Mono",
            "Fira Code",
            "Source Code Pro",
            "Courier New",
            "Lucida Console",
            "Segoe UI Mono"
        };

        private static readonly double[] FontSizes = { 10, 11, 12, 13, 14, 16, 18, 20 };

        /// <inheritdoc />
        public IEnumerable<AvailableFont> GetAvailableFonts()
        {
            var installedFontNames = System.Windows.Media.Fonts.SystemFontFamilies
                .Select(f => f.Source)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var result = new List<AvailableFont>();
            var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var fontName in KnownMonospaceFonts.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (installedFontNames.Contains(fontName) && !added.Contains(fontName))
                {
                    added.Add(fontName);
                    result.Add(new AvailableFont
                    {
                        FontFamilyName = fontName,
                        LocalizedDisplayName = fontName
                    });
                }
            }

            if (result.Count == 0)
            {
                result.Add(new AvailableFont
                {
                    FontFamilyName = "Consolas",
                    LocalizedDisplayName = "Consolas"
                });
            }

            return result;
        }

        /// <inheritdoc />
        public IEnumerable<AvailableFontSize> GetAvailableFontSizes()
        {
            return FontSizes.Select(size => new AvailableFontSize
            {
                Size = size,
                LocalizedDisplayName = size.ToString("F0")
            });
        }
    }
}
