using System;

namespace KID
{
    public partial class Sprite
    {
        /// <summary>
        /// Перемещает спрайт на (dx, dy).
        /// </summary>
        /// <param name="dx">Смещение по X.</param>
        /// <param name="dy">Смещение по Y.</param>
        /// <returns>Текущий спрайт (для цепочки вызовов).</returns>
        public virtual Sprite Move(double dx, double dy)
        {
            return InvokeOnUI(elements =>
            {
                if (dx == 0 && dy == 0) return this;

                _x += dx;
                _y += dy;

                foreach (var element in elements.All)
                {
                    executionScope.Environment.ThrowIfCancellationRequested();
                    if (element == null) continue;

                    var tt = GetOrCreateTranslateTransform(element);
                    tt.X += dx;
                    tt.Y += dy;
                }

                return this;
            });
        }

        /// <summary>
        /// Устанавливает абсолютную позицию anchor (X, Y), смещая все элементы на дельту.
        /// </summary>
        /// <param name="x">Новый X anchor.</param>
        /// <param name="y">Новый Y anchor.</param>
        protected virtual void SetPosition(double x, double y)
        {
            executionScope.CheckAccess(DispatcherManager.IsExecuting(executionScope));
            var dx = x - _x;
            var dy = y - _y;

            Move(dx, dy);
        }
    }
}

