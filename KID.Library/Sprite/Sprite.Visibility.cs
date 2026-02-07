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
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Graphics.Canvas == null) throw new ArgumentNullException("Canvas is null");

                foreach (var element in GraphicElements)
                {
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
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Graphics.Canvas == null) throw new ArgumentNullException("Canvas is null");

                foreach (var element in GraphicElements)
                {
                    if (element == null) continue;
                    element.Visibility = Visibility.Hidden;
                }

                _isVisible = false;
                return this;
            });
        }
    }
}

