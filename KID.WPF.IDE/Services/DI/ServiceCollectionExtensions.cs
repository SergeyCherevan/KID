using Microsoft.Extensions.DependencyInjection;
using KID.Services.CodeEditor;
using KID.Services.CodeEditor.Interfaces;
using KID.Services.Initialize;
using KID.Services.Initialize.Interfaces;
using KID.ViewModels;
using KID.ViewModels.Interfaces;
using System.Windows;
using KID.Services.CodeExecution.Interfaces;
using KID.Services.CodeExecution;
using KID.Services.CodeExecution.Contexts;
using KID.Services.Files;
using KID.Services.Files.Interfaces;
using KID.Services.Localization;
using KID.Services.Localization.Interfaces;
using KID.Services.Themes;
using KID.Services.Themes.Interfaces;
using KID.Services.Fonts;
using KID.Services.Fonts.Interfaces;
using KID.Services.Errors;
using KID.Services.Errors.Interfaces;
using KID.Services.WindowInterop;
using KID.Services.WindowInterop.Interfaces;

namespace KID.Services.DI
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddKIDServices(this IServiceCollection services)
        {
            // Services
            services.AddSingleton<ICodeCompiler, CSharpCompiler>();
            services.AddSingleton<ICodeRunner, DefaultCodeRunner>();
            services.AddSingleton<ICodeExecutionService, CodeExecutionService>();
            services.AddSingleton<CanvasTextBoxContextFabric>();

            // File Services
            services.AddSingleton<IFileDialogService, FileDialogService>();
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<ICodeFileService, CodeFileService>();

            // Window Configuration Services
            services.AddSingleton<IWindowConfigurationService, WindowConfigurationService>();
            services.AddSingleton<IWindowInitializationService, WindowInitializationService>();
            services.AddSingleton<IMainWindowWinAPIInteropService, MainWindowWinAPIInteropService>();

            // Code Editor Factory
            services.AddSingleton<ICodeEditorFactory, CodeEditorFactory>();

            // Localization Service
            services.AddSingleton<ILocalizationService, LocalizationService>();

            // Theme Service
            services.AddSingleton<IThemeService, ThemeService>();

            // Font Provider Service
            services.AddSingleton<IFontProviderService, FontProviderService>();

            // Error handling services
            services.AddSingleton<IAsyncOperationErrorHandler, AsyncOperationErrorHandler>();

            // ViewModels
            services.AddSingleton<IMainViewModel, MainViewModel>();
            services.AddSingleton<IMenuViewModel, MenuViewModel>();
            services.AddSingleton<ICodeEditorsViewModel, CodeEditorsViewModel>();
            services.AddSingleton<IConsoleOutputViewModel, ConsoleOutputViewModel>();
            services.AddSingleton<IGraphicsOutputViewModel, GraphicsOutputViewModel>();

            // MainWindow
            services.AddTransient<MainWindow>(sp => (Application.Current.MainWindow as MainWindow)!);
            services.AddSingleton<App>(sp => (Application.Current as App)!);

            return services;
        }
    }
}
