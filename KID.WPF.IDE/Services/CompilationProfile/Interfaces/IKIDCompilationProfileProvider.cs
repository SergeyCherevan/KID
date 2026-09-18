namespace KID.Services.CompilationProfile.Interfaces
{
    /// <summary>
    /// Предоставляет единый неизменяемый профиль анализа и компиляции пользовательского C#-кода.
    /// </summary>
    internal interface IKIDCompilationProfileProvider
    {
        /// <summary>
        /// Возвращает один и тот же полностью построенный профиль на протяжении lifetime провайдера.
        /// </summary>
        KIDCompilationProfile GetProfile();
    }
}
