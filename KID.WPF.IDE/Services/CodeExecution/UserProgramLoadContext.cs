using System.Reflection;
using System.Runtime.Loader;

namespace KID.Services.CodeExecution
{
    /// <summary>
    /// Выгружаемый контекст загрузки ровно одной пользовательской программы.
    /// </summary>
    /// <remarks>
    /// Пользовательская сборка загружается сюда явно, а все её зависимости разрешаются через
    /// default context. Так KID.Library, KID.WPF.IDE, BCL и WPF не загружаются повторно и
    /// сохраняют общую с IDE идентичность типов и статическое состояние.
    /// </remarks>
    internal sealed class UserProgramLoadContext : AssemblyLoadContext
    {
        /// <summary>
        /// Создаёт новый collectible context с уникальным диагностическим именем.
        /// </summary>
        public UserProgramLoadContext()
            : base($"KID.UserProgram.{Guid.NewGuid():N}", isCollectible: true)
        {
        }

        /// <summary>
        /// Передаёт разрешение зависимостей стандартному механизму runtime и default context.
        /// </summary>
        /// <param name="assemblyName">Имя запрошенной зависимости.</param>
        /// <returns>
        /// Всегда <see langword="null"/>: этот context не создаёт собственные копии host-сборок.
        /// </returns>
        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}
