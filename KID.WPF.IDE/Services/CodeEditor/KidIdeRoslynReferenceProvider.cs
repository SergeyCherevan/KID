using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KID.Services.CodeEditor.Interfaces;
using NAudio.Wave;

namespace KID.Services.CodeEditor
{
    /// <summary>
    /// Формирует набор сборок и типов для импортов через рефлексию над загруженным доменом — тот же источник, что и при компиляции кода в IDE (CSharpCompiler).
    /// </summary>
    public class KidIdeRoslynReferenceProvider : IRoslynReferenceProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<Assembly> GetAssemblies()
        {
            // Сборки по ProjectReference загружаются лениво. Обращение к типу заставляет загрузить сборку до GetAssemblies(), иначе KID.Library и NAudio могут ещё не быть в домене.
            _ = typeof(KID.Graphics).Assembly;
            _ = typeof(PlaybackState).Assembly;

            // Тот же фильтр, что и в CSharpCompiler: все загруженные сборки с непустым Location.
            // Сборки RoslynPad.* не включаем в ссылки документа — они для MEF редактора (additionalAssemblies в RoslynHost).
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Where(a =>
                {
                    var name = a.GetName().Name;
                    return name == null || !name.StartsWith("RoslynPad", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();
            return assemblies;
        }

        /// <inheritdoc />
        public IReadOnlyList<Type> GetTypeNamespaceImports()
        {
            var assemblies = GetAssemblies();
            var namespaceToType = new Dictionary<string, Type>(StringComparer.Ordinal);

            foreach (var assembly in assemblies)
            {
                Type[] types;
                try
                {
                    types = assembly.GetExportedTypes();
                }
                catch
                {
                    continue;
                }

                foreach (var type in types)
                {
                    var ns = type.Namespace;
                    if (string.IsNullOrEmpty(ns))
                        continue;
                    // Ограничиваем набор: только пространства, релевантные для скриптов в KID (System, KID, NAudio).
                    if (!IsRelevantNamespace(ns))
                        continue;
                    if (!namespaceToType.ContainsKey(ns))
                        namespaceToType[ns] = type;
                }
            }

            return namespaceToType.Values.ToList();
        }

        private static bool IsRelevantNamespace(string ns)
        {
            return ns.StartsWith("System", StringComparison.Ordinal) ||
                   ns.StartsWith("KID", StringComparison.Ordinal) ||
                   ns.StartsWith("NAudio", StringComparison.Ordinal);
        }
    }
}
