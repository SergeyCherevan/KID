using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Threading;
using KID.Services.Diagnostics;
using KID.Services.DI;
using KID.Services.Initialize.Interfaces;

namespace KID
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        private Serilog.ILogger? bootstrapLogger;
        private CrashReportWriter? crashReportWriter;
        private GlobalExceptionHandler? globalExceptionHandler;
        private int fatalShutdownStarted;
        private bool globalHandlersRegistered;

        protected override void OnStartup(StartupEventArgs e)
        {
            bootstrapLogger = LoggingConfiguration.CreateBootstrapLogger();
            crashReportWriter = new CrashReportWriter();
            globalExceptionHandler = new GlobalExceptionHandler(bootstrapLogger, crashReportWriter);
            RegisterGlobalExceptionHandlers();

            try
            {
                var serviceCollection = new ServiceCollection();
                LoggingConfiguration.AddTo(serviceCollection, bootstrapLogger);
                serviceCollection.AddSingleton(crashReportWriter);
                serviceCollection.AddKIDServices();
                ServiceProvider = serviceCollection.BuildServiceProvider(
                    new ServiceProviderOptions
                    {
                        ValidateOnBuild = true,
                        ValidateScopes = true
                    });

                bootstrapLogger.Information(
                    "Application DI initialized. EventId={EventId}",
                    DiagnosticEventIds.ApplicationStarted.Id);

                base.OnStartup(e);
            }
            catch (Exception exception)
            {
                HandleStartupFailure(exception);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            var fatalShutdown = Volatile.Read(ref fatalShutdownStarted) != 0;

            if (!fatalShutdown)
            {
                // Сохраняем настройки перед выходом (FontFamily/FontSize обновляются через SetFontAsync при смене шрифта).
                try
                {
                    var settingsService = ServiceProvider.GetRequiredService<IWindowConfigurationService>();
                    ServiceProvider.GetRequiredService<JoinableTaskFactory>().Run(
                        () => settingsService.SaveSettingsAsync());
                }
                catch (Exception exception)
                {
                    bootstrapLogger?.Error(
                        exception,
                        "Settings save failed during application exit. EventId={EventId} Operation={Operation}",
                        DiagnosticEventIds.ApplicationExitSaveFailed.Id,
                        "SaveSettingsOnExit");
                }
            }

            try
            {
                if (ServiceProvider is IDisposable disposable)
                    disposable.Dispose();
            }
            catch (Exception exception)
            {
                bootstrapLogger?.Error(exception, "Service provider disposal failed during application exit.");
            }

            try
            {
                bootstrapLogger?.Information(
                    "Application exiting. EventId={EventId} FatalShutdown={FatalShutdown}",
                    DiagnosticEventIds.ApplicationExiting.Id,
                    fatalShutdown);
                base.OnExit(e);
            }
            finally
            {
                UnregisterGlobalExceptionHandlers();
                if (bootstrapLogger is IDisposable disposableLogger)
                    disposableLogger.Dispose();
            }
        }

        private void RegisterGlobalExceptionHandlers()
        {
            if (globalHandlersRegistered)
                return;

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            globalHandlersRegistered = true;
        }

        private void UnregisterGlobalExceptionHandlers()
        {
            if (!globalHandlersRegistered)
                return;

            DispatcherUnhandledException -= OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            globalHandlersRegistered = false;
        }

        private void OnDispatcherUnhandledException(
            object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            if (Interlocked.Exchange(ref fatalShutdownStarted, 1) != 0)
            {
                e.Handled = true;
                return;
            }

            var report = globalExceptionHandler?.RecordFatal(
                "DispatcherUnhandledException",
                e.Exception,
                eventId: DiagnosticEventIds.DispatcherUnhandled);

            e.Handled = true;
            try
            {
                MessageBox.Show(
                    BuildCrashMessage(report),
                    "KID",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception dialogException)
            {
                bootstrapLogger?.Error(dialogException, "Fatal error dialog could not be shown.");
            }
            finally
            {
                try
                {
                    Shutdown(-1);
                }
                catch (Exception shutdownException)
                {
                    bootstrapLogger?.Fatal(shutdownException, "Application shutdown after fatal error failed.");
                }
            }
        }

        private void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception
                ?? new InvalidOperationException(e.ExceptionObject?.ToString() ?? "Unknown AppDomain exception.");
            globalExceptionHandler?.RecordFatal(
                "AppDomain.UnhandledException",
                exception,
                eventId: DiagnosticEventIds.AppDomainUnhandled);
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            if (globalExceptionHandler != null)
                globalExceptionHandler.RecordUnobservedTask(e.Exception);
            else
                bootstrapLogger?.Error(e.Exception, "Unobserved task exception before diagnostics initialization.");

            e.SetObserved();
        }

        private void HandleStartupFailure(Exception exception)
        {
            Interlocked.Exchange(ref fatalShutdownStarted, 1);
            var report = globalExceptionHandler?.RecordFatal(
                "ApplicationStartup",
                exception,
                eventId: DiagnosticEventIds.ApplicationStartupFailed);

            try
            {
                MessageBox.Show(
                    BuildCrashMessage(report),
                    "KID",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception dialogException)
            {
                bootstrapLogger?.Error(dialogException, "Startup failure dialog could not be shown.");
            }

            try
            {
                Shutdown(-1);
            }
            catch (Exception shutdownException)
            {
                bootstrapLogger?.Fatal(shutdownException, "Application shutdown after startup failure failed.");
            }
        }

        private static string BuildCrashMessage(CrashReportInfo? report) =>
            report?.Path == null
                ? "KID завершил работу из-за непредвиденной ошибки. " +
                  $"Идентификатор: {report?.CrashId ?? "неизвестен"}."
                : "KID завершил работу из-за непредвиденной ошибки. " +
                  $"Идентификатор: {report.CrashId}. Отчёт сохранён в: {report.Path}";
    }
}
