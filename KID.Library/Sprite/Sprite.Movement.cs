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
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Graphics.Canvas == null) throw new ArgumentNullException("Canvas is null");

                _x += dx;
                _y += dy;

                foreach (var element in GraphicElements)
                {
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
            var dx = x - _x;
            var dy = y - _y;

            if (dx == 0 && dy == 0)
                return;

            Move(dx, dy);
        }
    }
}

