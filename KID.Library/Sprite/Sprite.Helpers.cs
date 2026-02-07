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

        private static bool IsElementVisible(UIElement element)
        {
            return element.Visibility == Visibility.Visible;
        }

        private static Rect GetElementBoundsInCanvas(UIElement element, Canvas canvas)
        {
            if (element == null)
                return Rect.Empty;

            try
            {
                // 1) локальные bounds элемента
                var localBounds = VisualTreeHelper.GetDescendantBounds(element);
                if (localBounds.IsEmpty)
                    return Rect.Empty;

                // 2) перевод в координаты canvas (учитывает RenderTransform)
                var transformToCanvas = element.TransformToAncestor(canvas);
                return transformToCanvas.TransformBounds(localBounds);
            }
            catch
            {
                // Элемент может быть не в визуальном дереве или ещё не измерен.
                return Rect.Empty;
            }
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

