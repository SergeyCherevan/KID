using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KID
{
    public partial class Sprite
    {
        private readonly Dictionary<UIElement, TranslateTransform> _translateByElement = new();

        private sealed record SpriteElements(Canvas? Canvas, List<UIElement> All, List<UIElement> Attached);

        // GraphicElements is mutable: validate the entire snapshot before touching any element.
        private SpriteElements ValidateElements()
        {
            executionScope.CheckAccess(DispatcherManager.IsExecuting(executionScope));
            Canvas? canvas = null;
            List<UIElement> all = [];
            List<UIElement> attached = [];
            foreach (var element in GraphicElements)
            {
                executionScope.Environment.ThrowIfCancellationRequested();
                if (element == null) continue;
                GraphicsElementAccess.ValidateDispatcher(element, executionScope);
                var parent = GraphicsElementAccess.GetParentCanvas(element);
                if (parent != null)
                {
                    if (canvas != null && !ReferenceEquals(canvas, parent))
                        throw new InvalidOperationException("All attached sprite elements must belong to the same Canvas.");
                    canvas = parent;
                    attached.Add(element);
                }
                all.Add(element);
            }
            executionScope.Environment.ThrowIfCancellationRequested();
            return new SpriteElements(canvas, all, attached);
        }

        private static bool IsElementVisible(UIElement element)
        {
            return element.Visibility == Visibility.Visible;
        }

        private static Rect GetElementBoundsInCanvas(UIElement element, Canvas canvas)
        {
            var localBounds = VisualTreeHelper.GetDescendantBounds(element);
            if (localBounds.IsEmpty) return Rect.Empty;
            return element.TransformToAncestor(canvas).TransformBounds(localBounds);
        }

        private TranslateTransform GetOrCreateTranslateTransform(UIElement element)
        {
            if (_translateByElement.TryGetValue(element, out var existing))
                return existing;

            var current = element.RenderTransform;

            // Если уже есть TranslateTransform — используем его напрямую.
            if (current is TranslateTransform tt)
            {
                _translateByElement[element] = tt;
                return tt;
            }

            // Если есть TransformGroup — добавим туда TranslateTransform.
            if (current is TransformGroup group)
            {
                var newTt = new TranslateTransform();
                group.Children.Add(newTt);
                _translateByElement[element] = newTt;
                return newTt;
            }

            // Иначе — создаём TransformGroup, сохраняя текущий RenderTransform (если был).
            var newGroup = new TransformGroup();
            if (current != null && current != Transform.Identity)
                newGroup.Children.Add(current);

            var translate = new TranslateTransform();
            newGroup.Children.Add(translate);

            element.RenderTransform = newGroup;
            _translateByElement[element] = translate;
            return translate;
        }
    }
}

