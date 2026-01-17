namespace KID
{
    public static partial class Graphics
    {
        public static ColorType FillColor
        {
            get => new ColorType(() => fillBrush);
            set
            {
                DispatcherManager.InvokeOnUI(() =>
                {
                    fillBrush = value.CreateBrush();
                });
            }
        }

        public static ColorType StrokeColor
        {
            get => new ColorType(() => strokeBrush);
            set
            {
                DispatcherManager.InvokeOnUI(() =>
                {
                    strokeBrush = value.CreateBrush();
                });
            }
        }

        public static ColorType Color
        {
            get => new ColorType(() => fillBrush);
            set
            {
                DispatcherManager.InvokeOnUI(() =>
                {
                    var brush = value.CreateBrush();
                    fillBrush = brush;
                    strokeBrush = brush;
                });
            }
        }
    }
}

