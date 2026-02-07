using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace KID
{
    public partial class Sprite
    {
        /// <summary>
        /// Детектирует столкновения текущего спрайта с указанными спрайтами.
        /// </summary>
        /// <param name="sprites">Список спрайтов, с которыми нужно проверить столкновения.</param>
        /// <returns>Список обнаруженных столкновений.</returns>
        public virtual List<Collision> DetectCollisions(List<Sprite> sprites)
        {
            return DispatcherManager.InvokeOnUI(() =>
            {
                if (Graphics.Canvas == null) throw new ArgumentNullException("Canvas is null");
                var canvas = Graphics.Canvas;

                var result = new List<Collision>();
                Collisions = result;

                if (!_isVisible)
                    return result;

                if (sprites == null || sprites.Count == 0)
                    return result;

                var thisSpriteBounds = GetSpriteBoundsInCanvas(this, canvas);
                if (thisSpriteBounds.IsEmpty)
                    return result;

                foreach (var other in sprites.Where(s => s != null))
                {
                    if (ReferenceEquals(this, other))
                        continue;

                    if (!other._isVisible)
                        continue;

                    var otherBounds = GetSpriteBoundsInCanvas(other, canvas);
                    if (otherBounds.IsEmpty)
                        continue;

                    if (!thisSpriteBounds.IntersectsWith(otherBounds))
                        continue;

                    // Попарно проверяем элементы
                    foreach (var e1 in GraphicElements)
                    {
                        if (e1 == null || !IsElementVisible(e1))
                            continue;

                        var r1 = GetElementBoundsInCanvas(e1, canvas);
                        if (r1.IsEmpty)
                            continue;

                        foreach (var e2 in other.GraphicElements)
                        {
                            if (e2 == null || !IsElementVisible(e2))
                                continue;

                            var r2 = GetElementBoundsInCanvas(e2, canvas);
                            if (r2.IsEmpty)
                                continue;

                            if (!r1.IntersectsWith(r2))
                                continue;

                            result.Add(new Collision(
                                type: "Bounds",
                                firstSprite: this,
                                secondSprite: other,
                                firstElement: e1,
                                secondElement: e2));
                        }
                    }
                }

                Collisions = result;
                return result;
            });
        }

        private static Rect GetSpriteBoundsInCanvas(Sprite sprite, Canvas canvas)
        {
            Rect? union = null;

            foreach (var element in sprite.GraphicElements)
            {
                if (element == null || !IsElementVisible(element))
                    continue;

                var bounds = GetElementBoundsInCanvas(element, canvas);
                if (bounds.IsEmpty)
                    continue;

                union = union == null ? bounds : Rect.Union(union.Value, bounds);
            }

            return union ?? Rect.Empty;
        }
    }
}

