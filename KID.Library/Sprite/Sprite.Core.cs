using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace KID
{
    /// <summary>
    /// Спрайт — объект на графическом поле, состоящий из нескольких графических элементов.
    /// </summary>
    public partial class Sprite
    {
        // Спрайт сохраняет owner создания: ссылка из старой программы не получает новый scope.
        private readonly ExecutionDispatcherScope executionScope = DispatcherManager.GetScope();
        private T InvokeOnUI<T>(Func<T> action) => DispatcherManager.InvokeOnUI(executionScope, action);

        private static List<UIElement> CollectElements(IEnumerable<UIElement>? elements)
        {
            var result = new List<UIElement>();
            if (elements == null) return result;
            foreach (var element in elements)
            {
                DispatcherManager.CheckStop();
                if (element != null) result.Add(element);
            }
            return result;
        }
        private double _x;
        private double _y;
        private bool _isVisible = true;

        /// <summary>
        /// Видим ли спрайт (влияет на Visibility всех его элементов).
        /// </summary>
        public virtual bool IsVisible
        {
            get => _isVisible;
            set
            {
                executionScope.CheckAccess(DispatcherManager.IsExecuting(executionScope));
                if (_isVisible == value)
                    return;

                if (value)
                    Show();
                else
                    Hide();
            }
        }

        /// <summary>
        /// X координата точки-опоры (anchor) спрайта.
        /// </summary>
        public virtual double X
        {
            get => _x;
            set => SetPosition(value, Y);
        }

        /// <summary>
        /// Y координата точки-опоры (anchor) спрайта.
        /// </summary>
        public virtual double Y
        {
            get => _y;
            set => SetPosition(X, value);
        }

        /// <summary>
        /// Позиция anchor спрайта.
        /// </summary>
        public virtual Point Position
        {
            get => new Point(X, Y);
            set => SetPosition(value.X, value.Y);
        }

        /// <summary>
        /// Список графических элементов, из которых состоит спрайт.
        /// </summary>
        public virtual List<UIElement> GraphicElements { get; set; } = [];

        /// <summary>
        /// Последний рассчитанный список столкновений для этого спрайта.
        /// </summary>
        public virtual List<Collision> Collisions { get; set; } = [];

        /// <summary>
        /// Создаёт спрайт с anchor в (0, 0).
        /// </summary>
        public Sprite()
        {
        }

        /// <summary>
        /// Создаёт спрайт с anchor в (x, y) и заданными графическими элементами.
        /// </summary>
        /// <param name="x">X координата anchor.</param>
        /// <param name="y">Y координата anchor.</param>
        /// <param name="graphicElements">Графические элементы спрайта.</param>
        public Sprite(double x, double y, params UIElement[] graphicElements)
        {
            SetPosition(x, y);
            GraphicElements = CollectElements(graphicElements);
        }

        /// <summary>
        /// Создаёт спрайт с anchor в (x, y) и заданными графическими элементами.
        /// </summary>
        /// <param name="x">X координата anchor.</param>
        /// <param name="y">Y координата anchor.</param>
        /// <param name="graphicElements">Графические элементы спрайта.</param>
        public Sprite(double x, double y, IEnumerable<UIElement> graphicElements)
        {
            SetPosition(x, y);
            GraphicElements = CollectElements(graphicElements);
        }

        /// <summary>
        /// Создаёт спрайт с anchor в (x, y) и заданным изображением.
        /// </summary>
        /// <param name="x">X координата anchor.</param>
        /// <param name="y">Y координата anchor.</param>
        /// <param name="imagePath">Путь к изображению.</param>
        public Sprite(double x, double y, string imagePath)
        {
            SetPosition(x, y);
            GraphicElements = [Graphics.Image(x, y, imagePath)];
        }
    }
}

