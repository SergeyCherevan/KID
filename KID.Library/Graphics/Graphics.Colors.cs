namespace KID
{
    public static partial class Graphics
    {
        public static ColorType FillColor
        {
            get => DispatcherManager.InvokeOnUI(() => { var brush = fillBrush; return new ColorType(() => brush); });
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
            get => DispatcherManager.InvokeOnUI(() => { var brush = strokeBrush; return new ColorType(() => brush); });
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
            get => DispatcherManager.InvokeOnUI(() => { var brush = fillBrush; return new ColorType(() => brush); });
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

