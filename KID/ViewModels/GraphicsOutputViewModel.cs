using KID.ViewModels.Infrastructure;
using KID.ViewModels.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace KID.ViewModels
{
    public class GraphicsOutputViewModel : ViewModelBase, IGraphicsOutputViewModel
    {
        public Canvas GraphicsCanvasControl { get; private set; } = null!;

        public double DefaultOutputViewMinWidth { get; private set; }

        public double DefaultOutputViewMinHeight { get; private set; }

        private ColumnDefinition? outputColumn;
        private RowDefinition? graphicsRow;
        private bool defaultsCaptured;

        public void Initialize(Canvas graphicsCanvasControl)
        {
            GraphicsCanvasControl = graphicsCanvasControl ?? throw new ArgumentNullException(nameof(graphicsCanvasControl));

            // Best-effort: try capture immediately; if the window is not available yet, capture on Loaded.
            TryCaptureDefaultMinSizeFromXaml();
            GraphicsCanvasControl.Loaded += (_, __) => TryCaptureDefaultMinSizeFromXaml();
        }

        public void Clear()
        {
            GraphicsCanvasControl?.Children.Clear();
        }

        public void ResetOutputViewMinSizeToDefault()
        {
            if (!defaultsCaptured)
                TryCaptureDefaultMinSizeFromXaml();

            if (outputColumn == null || graphicsRow == null)
                return;

            outputColumn.MinWidth = DefaultOutputViewMinWidth;
            graphicsRow.MinHeight = DefaultOutputViewMinHeight;
        }

        private void TryCaptureDefaultMinSizeFromXaml()
        {
            if (GraphicsCanvasControl == null)
                return;

            var window = Window.GetWindow(GraphicsCanvasControl);
            if (window == null)
                return;

            var workspaceGrid = window.FindName("WorkspaceGrid") as Grid;
            var outputGrid = window.FindName("OutputGrid") as Grid;
            if (workspaceGrid?.ColumnDefinitions == null || workspaceGrid.ColumnDefinitions.Count < 3)
                return;
            if (outputGrid?.RowDefinitions == null || outputGrid.RowDefinitions.Count < 3)
                return;

            outputColumn = workspaceGrid.ColumnDefinitions[2];
            graphicsRow = outputGrid.RowDefinitions[2];

            if (!defaultsCaptured)
            {
                DefaultOutputViewMinWidth = outputColumn.MinWidth;
                DefaultOutputViewMinHeight = graphicsRow.MinHeight;
                defaultsCaptured = true;
            }
        }
    }
}
