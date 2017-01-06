using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using DocumentFormat.OpenXml.Presentation;

using Control = System.Windows.Controls.Control;

namespace KeywordDensity
{
	public class WordCloud : Control, INotifyPropertyChanged
	{
		static readonly Type _WordCloudType = typeof(WordCloud);

		public static readonly DependencyProperty EntriesProperty =
				DependencyProperty.Register("Entries",
				                            typeof(ObservableCollection<WordCloudEntry>),
				                            _WordCloudType,
				                            new PropertyMetadata(new ObservableCollection<WordCloudEntry>(), EntriesChanged));

		public static readonly DependencyProperty PlayingFieldProportionProperty =
				DependencyProperty.Register("PlayingFieldProportion",
				                            typeof(double),
				                            _WordCloudType,
				                            new PropertyMetadata(0.5D, RenderRelevantPropertyChanged));

		public static readonly DependencyProperty VerticalFractionProperty =
				DependencyProperty.Register("VerticalFraction",
				                            typeof(double),
				                            _WordCloudType,
				                            new PropertyMetadata(0.15D, RenderRelevantPropertyChanged));

		public static readonly DependencyProperty MinFontSizeProperty =
				DependencyProperty.Register("MinFontSize", typeof(double), _WordCloudType, new PropertyMetadata(6D, RenderRelevantPropertyChanged));

		public static readonly DependencyProperty FromColorProperty =
				DependencyProperty.Register("FromColor",
				                            typeof(SolidColorBrush),
				                            _WordCloudType,
				                            new PropertyMetadata(new SolidColorBrush(Colors.Blue), RenderRelevantPropertyChanged));

		public static readonly DependencyProperty ToColorProperty =
				DependencyProperty.Register("ToColor",
				                            typeof(SolidColorBrush),
				                            _WordCloudType,
				                            new PropertyMetadata(
					                            new SolidColorBrush(Colors.Green),
												RenderRelevantPropertyChanged));

		public static readonly DependencyProperty HighlightFromColorProperty =
				DependencyProperty.Register("HighlightFromColor",
											typeof(SolidColorBrush),
											_WordCloudType,
											new PropertyMetadata(new SolidColorBrush(Colors.Yellow), RenderRelevantPropertyChanged));

		public static readonly DependencyProperty HighlightToColorProperty =
				DependencyProperty.Register("HighlightToColor",
											typeof(SolidColorBrush),
											_WordCloudType,
											new PropertyMetadata(
												new SolidColorBrush(Colors.Fuchsia),
												RenderRelevantPropertyChanged));

		public static readonly DependencyProperty MaxWordsProperty =
				DependencyProperty.Register("MaxWords", typeof(int), _WordCloudType, new PropertyMetadata(100, RenderRelevantPropertyChanged));

		public static readonly DependencyProperty GroupByStemProperty =
				DependencyProperty.Register("GroupByStem",
				                            typeof(bool),
				                            _WordCloudType,
				                            new PropertyMetadata(false, RenderRelevantPropertyChanged));

		static WordCloud()
		{
			DefaultStyleKeyProperty.OverrideMetadata(_WordCloudType, new FrameworkPropertyMetadata(_WordCloudType));
		}

		readonly object _syncRoot = new object();
		CancellationTokenSource _renderCancel;

		StackPanel _layoutRoot;
		Image _image;

		#region Entries

		INotifyCollectionChanged _entries;
		public ObservableCollection<WordCloudEntry> Entries
		{
			get { return (ObservableCollection<WordCloudEntry>)GetValue(EntriesProperty); }
			set { SetValue(EntriesProperty, value); }
		}

		static void EntriesChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			((WordCloud)sender).OnEntriesChanged(e);
		}

		protected void OnEntriesChanged(DependencyPropertyChangedEventArgs e)
		{
			var entries = _entries;
			if (null != entries) entries.CollectionChanged -= EntriesCollectionChanged;

			entries = _entries = Entries;
			if (null != entries) entries.CollectionChanged += EntriesCollectionChanged;

			RegenerateCloud();
		}

		void EntriesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			RegenerateCloud();
		}

		#endregion

		#region PlayingFieldProportion

		public double PlayingFieldProportion
		{
			get { return (double)GetValue(PlayingFieldProportionProperty); }
			set { SetValue(PlayingFieldProportionProperty, value); }
		}

		#endregion

		#region VerticalFraction

		public double VerticalFraction
		{
			get { return (double)GetValue(VerticalFractionProperty); }
			set { SetValue(VerticalFractionProperty, value); }
		}

		#endregion

		#region MinFontSize

		public double MinFontSize
		{
			get { return (double)GetValue(MinFontSizeProperty); }
			set { SetValue(MinFontSizeProperty, value); }
		}

		#endregion

		#region FromColor

		public SolidColorBrush FromColor
		{
			get { return (SolidColorBrush)GetValue(FromColorProperty); }
			set { SetValue(FromColorProperty, value); }
		}

		#endregion

		#region ToColor

		public SolidColorBrush ToColor
		{
			get { return (SolidColorBrush)GetValue(ToColorProperty); }
			set { SetValue(ToColorProperty, value); }
		}

		#endregion

		#region HighlightFromColor

		public SolidColorBrush HighlightFromColor
		{
			get { return (SolidColorBrush)GetValue(HighlightFromColorProperty); }
			set { SetValue(HighlightFromColorProperty, value); }
		}

		#endregion

		#region HighlightToColor

		public SolidColorBrush HighlightToColor
		{
			get { return (SolidColorBrush)GetValue(HighlightToColorProperty); }
			set { SetValue(HighlightToColorProperty, value); }
		}

		#endregion

		#region MaxWords

		public int MaxWords
		{
			get { return (int)GetValue(MaxWordsProperty); }
			set { SetValue(MaxWordsProperty, value); }
		}

		#endregion

		#region GroupByStem

		public bool GroupByStem
		{
			get { return (bool)GetValue(GroupByStemProperty); }
			set { SetValue(GroupByStemProperty, value); }
		}

		#endregion

		static void RenderRelevantPropertyChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			((WordCloud)sender).RegenerateCloud();
		}

		void LayoutRootSizeChanged(object sender, SizeChangedEventArgs e)
		{
			RegenerateCloud();
		}

		public override void OnApplyTemplate()
		{
			_image = GetTemplateChild("Image") as Image ?? new Image();
			_layoutRoot = GetTemplateChild("LayoutRoot") as StackPanel ?? new StackPanel();
			_layoutRoot.SizeChanged += LayoutRootSizeChanged;
			RegenerateCloud();
			base.OnApplyTemplate();
		}


		async void RegenerateCloud()
		{
			var cts = new CancellationTokenSource();
			// Can't use Interlocked.Exchange() as CancellationTokenSource is a struct, and it doesn't work on structs.
			lock (_syncRoot)
			{
				_renderCancel?.Cancel();
				_renderCancel = cts;
			}

			var rendering = await Task.Run(() => GenerateImage(Entries?.ToList() ?? Enumerable.Empty<WordCloudEntry>(),
			                                                   PlayingFieldProportion,
			                                                   VerticalFraction,
			                                                   MinFontSize,
			                                                   FromColor,
			                                                   ToColor,
			                                                   HighlightFromColor,
			                                                   HighlightToColor,
			                                                   MaxWords,
			                                                   GroupByStem,
			                                                   cts.Token),
			                               cts.Token);

			
		}

		class GenerateResults
		{
			BitmapSource Bitmap { get; set; }
			QuadTree<Symbol>  Symbols { get; set; }
		}

		GenerateResults GenerateImage(
			IEnumerable<WordCloudEntry> entries,
			double playingFieldProportion,
			double verticalFraction,
			double minFontSize,
			SolidColorBrush fromColor,
			SolidColorBrush toColor,
			SolidColorBrush highlightFromColor,
			SolidColorBrush highlightToColor,
			int maxWords,
			bool groupByStem,
			CancellationToken cancellationToken)
		{
			// Use same seed every time so rendering same words gets same result.
			var random = new Random(54321);
			var symbols =
					from e in entries
					group e by groupByStem ? WordCloudEntry.StemGetter : WordCloudEntry.WordGetter
					into g
					select new Symbol
					{
						DisplayText = g.First().Word,
						Count = g.Sum(ge => ge.Count),
						IndividualWords = groupByStem ? from ge in g select Tuple.Create(ge.Word, ge.Count) : null,
						Highlight = g.Any(ge => ge.Highlight)
					};
			if (maxWords > 0 && maxWords < int.MaxValue)
				symbols = symbols.OrderByDescending(s => s.Highlight).ThenBy(s => s.Count).Take(maxWords).ToList();
			symbols = symbols.OrderByDescending(s => s.Count).ToList(); // No more of that deferred eval stuff!


		}

		Point GetSpiralPoint(int step, double growthRate = 7/(2*Math.PI))
		{
			const double stepRadians = 2*Math.PI/100;
			var theta = step*stepRadians;
			var radius = growthRate*theta;
			return new Point(radius * Math.Sin(theta), radius * Math.Cos(theta));
		}

		class Symbol
		{
			public string DisplayText { get; set; }
			public int Count { get; set; }
			public bool Highlight { get; set; }
			public bool Vertical { get; set; }
			public SolidColorBrush Color { get; set; }
			public IEnumerable<Tuple<string, int>> IndividualWords { get; set; }
			public Rect Bounds { get; set; }
		}
	}
}
