using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using KID.Services.CodeEditor.Interfaces;
using NAudio.Wave;
using RoslynPad.Roslyn;

namespace KID.Services.CodeEditor
{
    /// <summary>
    /// Реализация сервиса RoslynHost с настройками для IDE KID: ссылки на KID.Library, NAudio и WPF, импорты System и KID.
    /// </summary>
    public class RoslynHostService : IRoslynHostService
    {
        private RoslynHost? _host;

        /// <inheritdoc />
        public RoslynHost GetHost()
        {
            if (_host != null)
                return _host;

            // Сборки с непустым Location — иначе RoslynHostReferences не создаёт ссылку (MetadataReference.CreateFromFile не принимает пустой путь).
            static Assembly? LoadIfHasLocation(string assemblyName)
            {
                var a = Assembly.Load(assemblyName);
                return !string.IsNullOrEmpty(a.Location) ? a : null;
            }

            var assemblyList = new List<Assembly>
            {
                Assembly.Load("System.Runtime"),
                typeof(object).Assembly,
                typeof(System.Linq.Enumerable).Assembly,
                typeof(KID.Graphics).Assembly,
                typeof(PlaybackState).Assembly
            };
            // WPF: явная загрузка по имени, чтобы гарантированно получить сборки с Location (Ellipse, Shape и др. в PresentationFramework).
            if (LoadIfHasLocation("PresentationFramework") is { } pf) assemblyList.Add(pf);
            if (LoadIfHasLocation("PresentationCore") is { } pc) assemblyList.Add(pc);
            if (LoadIfHasLocation("WindowsBase") is { } wb) assemblyList.Add(wb);
            // Резерв: типы из уже загруженного процесса (если Load по имени не вернул с Location).
            if (!assemblyList.Contains(typeof(UIElement).Assembly) && !string.IsNullOrEmpty(typeof(UIElement).Assembly.Location))
                assemblyList.Add(typeof(UIElement).Assembly);
            if (!assemblyList.Contains(typeof(Brush).Assembly) && !string.IsNullOrEmpty(typeof(Brush).Assembly.Location))
                assemblyList.Add(typeof(Brush).Assembly);

            // Явно добавляем сборки рантайма и WPF, иначе Roslyn не разрешает ValueType, Ellipse и др. — красные подчёркивания.
            var references = RoslynHostReferences.NamespaceDefault.With(
                assemblyReferences: assemblyList,
                typeNamespaceImports: new[]
                {
                    typeof(object),
                    typeof(System.Console),
                    typeof(PlaybackState),
                    typeof(KID.Graphics)
                }
            );

            // Не добавлять typeof(RoslynHost).Assembly — хост уже включает её в состав по умолчанию; дублирование даёт CompositionFailedException (два экспорта DocumentationProviderService).
            _host = new RoslynHost(
                additionalAssemblies: new[]
                {
                    Assembly.Load("RoslynPad.Roslyn.Windows"),
                    Assembly.Load("RoslynPad.Editor.Windows")
                },
                references: references
            );

            return _host;
        }
    }
}
