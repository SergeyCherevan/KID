using System.Windows;
using System.Windows.Controls;

namespace KID;

public partial class Sprite
{
    /// <summary>Детектирует столкновения присоединённых элементов спрайтов одного Canvas.</summary>
    public virtual List<Collision> DetectCollisions(List<Sprite> sprites)
    {
        return InvokeOnUI(elements =>
        {
            // Validate all operands before visibility/empty/different-canvas shortcuts.
            List<(Sprite Sprite, SpriteElements Elements)> others = [];
            if (sprites != null)
            {
                foreach (var other in sprites)
                {
                    executionScope.Environment.ThrowIfCancellationRequested();
                    if (other == null || ReferenceEquals(this, other)) continue;
                    others.Add((other, other.ValidateElements()));
                }
            }

            List<Collision> result = [];
            Collisions = result;
            var canvas = elements.Canvas;
            if (!_isVisible || canvas == null) return result;
            var bounds = GetSpriteBoundsInCanvas(elements.Attached, canvas);
            if (bounds.IsEmpty) return result;

            foreach (var (other, otherElements) in others)
            {
                executionScope.Environment.ThrowIfCancellationRequested();
                if (!other._isVisible || !ReferenceEquals(canvas, otherElements.Canvas)) continue;
                var otherBounds = GetSpriteBoundsInCanvas(otherElements.Attached, canvas);
                if (otherBounds.IsEmpty || !bounds.IntersectsWith(otherBounds)) continue;
                foreach (var first in elements.Attached)
                {
                    executionScope.Environment.ThrowIfCancellationRequested();
                    if (!IsElementVisible(first)) continue;
                    var firstBounds = GetElementBoundsInCanvas(first, canvas);
                    if (firstBounds.IsEmpty) continue;
                    foreach (var second in otherElements.Attached)
                    {
                        executionScope.Environment.ThrowIfCancellationRequested();
                        if (!IsElementVisible(second)) continue;
                        var secondBounds = GetElementBoundsInCanvas(second, canvas);
                        if (!secondBounds.IsEmpty && firstBounds.IntersectsWith(secondBounds))
                            result.Add(new Collision("Bounds", this, other, first, second));
                    }
                }
            }
            return result;
        });
    }

    private Rect GetSpriteBoundsInCanvas(List<UIElement> elements, Canvas canvas)
    {
        var union = Rect.Empty;
        foreach (var element in elements)
        {
            executionScope.Environment.ThrowIfCancellationRequested();
            if (!IsElementVisible(element)) continue;
            var bounds = GetElementBoundsInCanvas(element, canvas);
            if (!bounds.IsEmpty) union.Union(bounds);
        }
        return union;
    }
}
