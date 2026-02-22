using System.Collections.Generic;
using System.Reflection;

namespace KID.Services.CodeEditor.Interfaces
{
    /// <summary>
    /// Предоставляет набор сборок и типов для глобальных импортов (usings), используемых RoslynHost в KID IDE при анализе и IntelliSense.
    /// </summary>
    public interface IRoslynReferenceProvider
    {
        /// <summary>
        /// Возвращает список сборок для ссылок проекта документа — те же, что доступны при компиляции и выполнении кода в IDE.
        /// </summary>
        /// <returns>Сборки с непустым <see cref="Assembly.Location"/> (без динамических).</returns>
        IReadOnlyList<Assembly> GetAssemblies();

        /// <summary>
        /// Возвращает по одному типу на пространство имён из загруженных сборок для глобальных usings в документе.
        /// </summary>
        /// <returns>Типы, по которым RoslynHostReferences формирует импорты.</returns>
        IReadOnlyList<Type> GetTypeNamespaceImports();
    }
}
