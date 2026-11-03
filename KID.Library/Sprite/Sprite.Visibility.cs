using System;
using System.Windows;

namespace KID
{
    public partial class Sprite
    {
        /// <summary>
        /// Показывает спрайт (делает все его элементы видимыми).
        /// </summary>
        /// <returns>Текущий спрайт (для цепочки вызовов).</returns>
        public virtual Sprite Show()
        {
            return InvokeOnUI(elements =>
            {
                foreach (var element in elements.All)
                {
                    executionScope.Environment.ThrowIfCancellationRequested();
                    if (element == null) continue;
                    element.Visibility = Visibility.Visible;
                }

                _isVisible = true;
                return this;
            });
        }

        /// <summary>
        /// Прячет спрайт (делает все его элементы невидимыми).
        /// </summary>
        /// <returns>Текущий спрайт (для цепочки вызовов).</returns>
        public virtual Sprite Hide()
        {
            return InvokeOnUI(elements =>
            {
                foreach (var element in elements.All)
                {
                    executionScope.Environment.ThrowIfCancellationRequested();
                    if (element == null) continue;
                    element.Visibility = Visibility.Hidden;
                }

                _isVisible = false;
                return this;
            });
        }
    }
}

