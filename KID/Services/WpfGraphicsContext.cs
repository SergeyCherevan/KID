using System;
using System.Windows.Controls;
using KID.Services.Interfaces;

namespace KID.Services
{
    /// <summary>
    /// Реализация графического контекста для WPF Canvas
    /// </summary>
    public class WpfGraphicsContext : IGraphicsContext
    {
        public void Initialize(object graphicsTarget)
        {
            if (graphicsTarget is Canvas canvas)
            {
                Graphics.Init(canvas);
            }
            else
            {
                throw new ArgumentException(
                    $"Ожидался Canvas, получен {graphicsTarget?.GetType().Name ?? "null"}", 
                    nameof(graphicsTarget));
            }
        }
    }
}

