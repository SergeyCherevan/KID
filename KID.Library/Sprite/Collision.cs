using System.Windows;

namespace KID
{
    /// <summary>
    /// Описывает столкновение между двумя спрайтами и их графическими элементами.
    /// </summary>
    public class Collision
    {
        /// <summary>
        /// Тип столкновения (например, "Bounds").
        /// </summary>
        public virtual string Type { get; set; } = "Bounds";

        /// <summary>
        /// Пара спрайтов-участников столкновения.
        /// </summary>
        public virtual (Sprite First, Sprite Second) SpritesPair { get; set; } = (null!, null!);

        /// <summary>
        /// Пара конкретных графических элементов, которые пересеклись.
        /// </summary>
        public virtual (UIElement First, UIElement Second) GraphicElementsPair { get; set; } = (null!, null!);

        /// <summary>
        /// Дополнительная информация (произвольные данные/логика ученика).
        /// </summary>
        public virtual dynamic? AdditionalInfo { get; set; }

        /// <summary>
        /// Создаёт пустое столкновение.
        /// </summary>
        public Collision()
        {
        }

        /// <summary>
        /// Создаёт столкновение с заданными параметрами.
        /// </summary>
        /// <param name="type">Тип столкновения.</param>
        /// <param name="firstSprite">Первый спрайт.</param>
        /// <param name="secondSprite">Второй спрайт.</param>
        /// <param name="firstElement">Первый графический элемент.</param>
        /// <param name="secondElement">Второй графический элемент.</param>
        /// <param name="additionalInfo">Дополнительная информация.</param>
        public Collision(
            string type,
            Sprite firstSprite,
            Sprite secondSprite,
            UIElement firstElement,
            UIElement secondElement,
            dynamic? additionalInfo = null)
        {
            Type = type;
            SpritesPair = (firstSprite, secondSprite);
            GraphicElementsPair = (firstElement, secondElement);
            AdditionalInfo = additionalInfo;
        }
    }
}

