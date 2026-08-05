using System;
using System.Windows.Input;

namespace KID.ViewModels.Infrastructure
{
    /// <summary>
    /// Интерфейс команды с возможностью принудительного обновления состояния CanExecute.
    /// </summary>
    public interface IRaisableCommand : ICommand
    {
        /// <summary>
        /// Вызывает уведомление об изменении возможности выполнения команды.
        /// </summary>
        void RaiseCanExecuteChanged();
    }

    public class RelayCommand : IRaisableCommand
    {
        private readonly Action execute;
        private readonly Func<bool>? canExecute;
        private event EventHandler? canExecuteChanged;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => canExecuteChanged += value;
            remove => canExecuteChanged -= value;
        }

        /// <inheritdoc />
        public void RaiseCanExecuteChanged()
        {
            canExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool CanExecute(object? parameter)
        {
            return canExecute == null || canExecute();
        }

        public void Execute(object? parameter)
        {
            execute();
        }
    }

    public class RelayCommand<T> : IRaisableCommand
    {
        private readonly Action<T> execute;
        private readonly Func<T, bool>? canExecute;
        private event EventHandler? canExecuteChanged;

        public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => canExecuteChanged += value;
            remove => canExecuteChanged -= value;
        }

        /// <inheritdoc />
        public void RaiseCanExecuteChanged()
        {
            canExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool CanExecute(object? parameter)
        {
            if (!TryGetParameter(parameter, out var typedParameter))
                return false;

            return canExecute == null || canExecute(typedParameter);
        }

        public void Execute(object? parameter)
        {
            if (!TryGetParameter(parameter, out var typedParameter))
                throw new ArgumentException(
                    $"Command parameter must be of type {typeof(T).FullName}.",
                    nameof(parameter));

            execute(typedParameter);
        }

        private static bool TryGetParameter(object? parameter, out T typedParameter)
        {
            if (parameter is T value)
            {
                typedParameter = value;
                return true;
            }

            if (parameter == null && default(T) == null)
            {
                typedParameter = default!;
                return true;
            }

            typedParameter = default!;
            return false;
        }
    }
}
