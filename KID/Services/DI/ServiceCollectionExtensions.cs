using Microsoft.Extensions.DependencyInjection;
using KID.Services;
using KID.Services.Interfaces;
using KID.Services.Initialize;
using KID.Services.Initialize.Interfaces;
using KID.ViewModels;
using KID.ViewModels.Interfaces;

namespace KID.Services.DI
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddKIDServices(this IServiceCollection services)
        {
            // Services
            services.AddSingleton<ICodeCompiler, CSharpCompiler>();
            services.AddSingleton<ICodeRunner, DefaultCodeRunner>();
            services.AddSingleton<CodeExecutionService>();
            services.AddSingleton<ConsoleRedirector>();

            // Window Configuration Services
            services.AddSingleton<IWindowConfigurationService, WindowConfigurationService>();
            services.AddSingleton<IWindowInitializationService, WindowInitializationService>();

            // ViewModels
            services.AddSingleton<IMainViewModel, MainViewModel>();
            services.AddSingleton<IMenuViewModel, MenuViewModel>();
            services.AddSingleton<ICodeEditorViewModel, CodeEditorViewModel>();
            services.AddSingleton<IConsoleOutputViewModel, ConsoleOutputViewModel>();
            services.AddSingleton<IGraphicsOutputViewModel, GraphicsOutputViewModel>();

            return services;
        }
    }
}
