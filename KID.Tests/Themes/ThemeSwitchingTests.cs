using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KID.Controls;
using KID.Services.CodeEditor;
using KID.Services.Themes;
using KID.Tests.Infrastructure;

namespace KID.Tests.Themes;

public sealed class ThemeSwitchingTests
{
    [Fact]
    public async Task ExistingControlsAndCapturedMenuStyle_UpdateAcrossBothGenerations()
    {
        // Не создаём Application: ресурсы принадлежат локальному host и не оставляют
        // глобальное WPF-приложение на завершённом Dispatcher других тестов.
        await StaTest.RunAsync(async () =>
        {
            var provider = new ThemeProviderService();
            var host = new UserControl();
            var sharedResource = new object();
            var sharedDictionary = new ResourceDictionary { ["SharedResource"] = sharedResource };
            var hostThemeContainer = new ThemeResourceContainer();
            hostThemeContainer.MergedDictionaries.Add(new ResourceDictionary());
            host.Resources.MergedDictionaries.Add(sharedDictionary);
            host.Resources.MergedDictionaries.Add(hostThemeContainer);
            var panel = new StackPanel();
            host.Content = panel;
            var item = new object();
            host.DataContext = new TabSelection(item);
            var tab = new EditorTabBorder { DataContext = item, HorizontalAlignment = HorizontalAlignment.Left };
            var tabContent = new DockPanel();
            tab.Child = tabContent;
            var text = new TextBlock { Text = "Example.cs" };
            var close = new Button { Content = "×" };
            tabContent.Children.Add(text);
            tabContent.Children.Add(close);
            var minimize = new Button { Content = "—" };
            var menu = new Menu();
            var header = new MenuItem { Header = "View" };
            var child = new MenuItem { Header = "Themes", IsCheckable = true, IsChecked = true };
            header.Items.Add(child);
            menu.Items.Add(header);
            var context = new ContextMenu();
            var contextThemeContainer = new ThemeResourceContainer();
            contextThemeContainer.MergedDictionaries.Add(new ResourceDictionary());
            context.Resources.MergedDictionaries.Add(sharedDictionary);
            context.Resources.MergedDictionaries.Add(contextThemeContainer);
            context.Items.Add(new MenuItem { Header = "Close" });
            panel.Children.Add(menu);
            panel.Children.Add(minimize);
            panel.Children.Add(tab);
            var scroll = new ScrollViewer
            {
                Width = 300,
                Height = 80,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                Content = new Border { Width = 1000, Height = 500 }
            };
            panel.Children.Add(scroll);
            tab.SetResourceReference(FrameworkElement.StyleProperty, "EditorTabBorderStyle");
            text.SetResourceReference(FrameworkElement.StyleProperty, "TabTextStyle");
            close.SetResourceReference(FrameworkElement.StyleProperty, "TabCloseButtonStyle");
            minimize.SetResourceReference(FrameworkElement.StyleProperty, "MinimizeButtonStyle");

            var order = new[] { 0, 1, 2, 3, 0 };
            Style? capturedMenuStyle = null;
            foreach (var index in order)
            {
                var theme = provider.GetAvailableThemes()[index];
                var dictionary = new ResourceDictionary
                {
                    Source = new Uri($"/KID.WPF.IDE;component/{theme.ResourcePath}", UriKind.Relative)
                };
                ThemeService.ReplaceThemeDictionary(host.Resources, dictionary);
                ThemeService.ReplaceThemeDictionary(context.Resources, dictionary);
                Assert.Same(sharedResource, host.FindResource("SharedResource"));
                Assert.Same(sharedResource, context.FindResource("SharedResource"));
                Assert.Same(dictionary, Assert.Single(hostThemeContainer.MergedDictionaries));
                Assert.Same(dictionary, Assert.Single(contextThemeContainer.MergedDictionaries));
                Assert.Equal(2, host.Resources.MergedDictionaries.Count);
                Assert.Equal(2, context.Resources.MergedDictionaries.Count);
                if (capturedMenuStyle == null)
                {
                    capturedMenuStyle = new Style(typeof(MenuItem), (Style)dictionary[typeof(MenuItem)]);
                    // MenuView captures its BasedOn once; its values must still follow the theme.
                    child.Style = capturedMenuStyle;
                }
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                host.Measure(new Size(600, 200));
                host.Arrange(new Rect(0, 0, 600, 200));
                host.UpdateLayout();
                minimize.ApplyTemplate();
                close.ApplyTemplate();
                child.ApplyTemplate();
                header.ApplyTemplate();
                context.ApplyTemplate();
                var modern = index >= 2;
                Assert.Equal(modern, context.Tag);
                if (modern)
                    Assert.Same(dictionary["ModernContextMenuTemplate"], context.Template);
                foreach (var menuHeader in new[] { header })
                {
                    menuHeader.ApplyTemplate();
                    var popup = Assert.IsType<Popup>(menuHeader.Template.FindName("PART_Popup", menuHeader));
                    var popupBorder = Assert.IsType<Border>(popup.Child);
                    Assert.Equal(BrushColor(dictionary["MenuPopupBrush"]), BrushColor(popupBorder.Background));
                    Assert.Equal(modern ? 6 : 2, popupBorder.CornerRadius.TopLeft);
                    popupBorder.Measure(new Size(300, 100));
                    popupBorder.Arrange(new Rect(0, 0, 300, 100));
                    new RenderTargetBitmap(300, 100, 96, 96, PixelFormats.Pbgra32).Render(popupBorder);
                }
                context.Measure(new Size(300, 100));
                context.Arrange(new Rect(0, 0, 300, 100));
                new RenderTargetBitmap(300, 100, 96, 96, PixelFormats.Pbgra32).Render(context);
                scroll.ApplyTemplate();
                var scrollBars = VisualChildren<ScrollBar>(scroll).ToArray();
                Assert.Equal(2, scrollBars.Length);
                foreach (var bar in scrollBars)
                {
                    bar.ApplyTemplate();
                    var track = Assert.IsType<Track>(bar.Template.FindName("PART_Track", bar));
                    Assert.Equal(bar.Orientation, track.Orientation);
                    Assert.Equal(bar.Maximum, track.Maximum);
                    Assert.Equal(bar.ViewportSize, track.ViewportSize);
                    if (modern)
                    {
                        Assert.Same(dictionary[bar.Orientation == Orientation.Vertical
                            ? "VerticalEditorScrollTemplate" : "HorizontalEditorScrollTemplate"], bar.Template);
                    }
                    else
                    {
                        Assert.Null(bar.Style); // Returning to VS2022 restores the system scrollbar.
                    }
                }
                scroll.ScrollToTop();
                scroll.ScrollToLeftEnd();
                scroll.UpdateLayout();
                ScrollBar.LineDownCommand.Execute(null,
                    scrollBars.Single(bar => bar.Orientation == Orientation.Vertical));
                ScrollBar.LineRightCommand.Execute(null,
                    scrollBars.Single(bar => bar.Orientation == Orientation.Horizontal));
                scroll.UpdateLayout();
                Assert.True(scroll.VerticalOffset > 0);
                Assert.True(scroll.HorizontalOffset > 0);
                Assert.Equal(modern, tab.UseCurvedShape);
                Assert.Equal(modern ? 44 : 40, minimize.Width);
                Assert.Equal(modern ? 30 : 28, minimize.Height);
                Assert.Equal(modern ? 24 : 18, close.Width);
                Assert.Equal(modern ? 4 : 0, tab.CornerRadius.TopLeft);
                // Compare actual content positions against the inner vertical edges,
                // not Padding alone: VS2026's curved edges sit 4 DIP inside its bounds.
                var sideInset = modern ? tab.CornerRadius.TopLeft : 0;
                var textStart = text.TranslatePoint(new Point(), tab).X;
                var closeStart = close.TranslatePoint(new Point(), tab).X;
                Assert.Equal(10, textStart - sideInset - tab.BorderThickness.Left, 6);
                Assert.Equal(8, closeStart - textStart - text.ActualWidth, 6);
                Assert.Equal(4, tab.ActualWidth - sideInset - tab.BorderThickness.Right
                    - closeStart - close.ActualWidth, 6);
                Assert.Equal(modern ? FontWeights.Normal : FontWeights.Bold, text.FontWeight);
                Assert.Equal(BrushColor(dictionary["TabActiveBrush"]), BrushColor(tab.Background));
                Assert.Equal(BrushColor(dictionary["TextBrush"]), BrushColor(child.Foreground));
                Assert.Equal(BrushColor(dictionary["TabActiveTextBrush"]), BrushColor(close.Foreground));
                Assert.Same(capturedMenuStyle, child.Style);
                Assert.Equal(modern, child.Tag);
                if (modern)
                {
                    Assert.NotNull(minimize.Tag);
                    Assert.IsAssignableFrom<Geometry>(minimize.Tag);
                    Assert.IsType<System.Windows.Shapes.Path>(close.Template.FindName("Glyph", close));
                }
                var colors = dictionary[ClassificationHighlightColorsProvider.CodeEditorClassificationColors];
                Assert.Equal(index switch
                {
                    0 => typeof(VS2022LightClassificationHighlightColors),
                    1 => typeof(VS2022DarkClassificationHighlightColors),
                    2 => typeof(VS2026LightClassificationHighlightColors),
                    _ => typeof(VS2026DarkClassificationHighlightColors)
                }, colors.GetType());

                // Render actual templates/curved geometry, not just the raw dictionary.
                var bitmap = new RenderTargetBitmap(600, 200, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(host);
            }
        });
    }

    private static Color BrushColor(object value) => Assert.IsType<SolidColorBrush>(value).Color;

    private static IEnumerable<T> VisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;
            foreach (var descendant in VisualChildren<T>(child))
                yield return descendant;
        }
    }

    private sealed record TabSelection(object CurrentFileTab);
}
