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
            GraphicElements = graphicElements?.Where(e => e != null).ToList() ?? [];
            SetPosition(x, y);
        }

        /// <summary>
        /// Создаёт спрайт с anchor в (x, y) и заданными графическими элементами.
        /// </summary>
        /// <param name="x">X координата anchor.</param>
        /// <param name="y">Y координата anchor.</param>
        /// <param name="graphicElements">Графические элементы спрайта.</param>
        public Sprite(double x, double y, IEnumerable<UIElement> graphicElements)
        {
            GraphicElements = graphicElements?.Where(e => e != null).ToList() ?? [];
            SetPosition(x, y);
        }

        /// <summary>
        /// Создаёт спрайт с anchor в (x, y) и заданным изображением.
        /// </summary>
        /// <param name="x">X координата anchor.</param>
        /// <param name="y">Y координата anchor.</param>
        /// <param name="imagePath">Путь к изображению.</param>
        public Sprite(double x, double y, string imagePath)
        {
            GraphicElements = [Graphics.Image(x, y, imagePath)];
            SetPosition(x, y);
        }
    }
}

