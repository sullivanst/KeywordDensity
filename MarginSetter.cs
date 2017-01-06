using System;
using System.Windows;
using System.Windows.Controls;

namespace KeywordDensity
{
	public class MarginSetter
	{
		public static readonly DependencyProperty MarginProperty =
				DependencyProperty.RegisterAttached("Margin",
				                                    typeof(Thickness),
				                                    typeof(MarginSetter),
				                                    new UIPropertyMetadata(new Thickness(), MarginChangedCallback));

		public static Thickness GetMargin(DependencyObject obj) => (Thickness)obj.GetValue(MarginProperty);

		public static void SetMargin(DependencyObject obj, Thickness value) => obj.SetValue(MarginProperty, value);

		public static void MarginChangedCallback(object sender, DependencyPropertyChangedEventArgs e)
		{
			var panel = sender as Panel;
			if (null != panel) panel.Loaded += panel_Loaded;
		}

		static void panel_Loaded(object sender, RoutedEventArgs e)
		{
			var panel = (Panel)sender;
			foreach (var child in panel.Children)
			{
				var fe = child as FrameworkElement;
				if (null != fe) fe.Margin = GetMargin(panel);
			}
		}
	}
}
