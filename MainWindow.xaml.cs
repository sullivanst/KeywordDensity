using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using Iveonik.Stemmers;

using Path = System.IO.Path;

namespace KeywordDensity
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		static readonly IStemmer _Stemmer = new EnglishStemmer();
		static readonly Task<HashSet<string>> _CommonStopwordsTask = LoadCommonStopwords();
		static readonly StringComparer _StringComparer = StringComparer.InvariantCultureIgnoreCase;

		HashSet<string> _additionalStopwords = new HashSet<string>();
		HashSet<string> _ignoreStopwords = new HashSet<string>();
		IDictionary<string, int> _wordCounts;
		IDictionary<string, int> _stemCounts;

		public MainWindow()
		{
			InitializeComponent();

		}

		async static Task<HashSet<string>> LoadCommonStopwords()
		{
			var stopwords = new HashSet<string>(_StringComparer);
			
			var stopwordsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StopWords.txt");
			if (await Task.Run(() => File.Exists(stopwordsPath)).ConfigureAwait(false))
			{
				// Yes, I know this is synchronously opening a file when we asynchronously tested for its existence
				// It makes sense though because we shed our context in the first await, and are now in async-land,
				// so while we're holding a thread, we have to anyway because there's no OpenTextAsync or anything like it,
				// and this is the best we can do
				// Besides, opening a file should be pretty quick.
				using (var rdr = File.OpenText(stopwordsPath))
				{
					string line;
					while (null != (line = await rdr.ReadLineAsync().ConfigureAwait(false)))
					{
						if (!string.IsNullOrWhiteSpace(line))
							stopwords.Add(_Stemmer.Stem(line.Trim()));
					}
				}
			}

			return stopwords;
		}

		// TODO: keyphrase support
		async Task<IDictionary<string, int>> CountWords(string pathToDocx)
		{
			WordprocessingDocument doc;
			using (var ms = new MemoryStream())
			{
				await File.OpenRead(pathToDocx).CopyToAsync(ms).ConfigureAwait(false);
				ms.Position = 0;
				doc = WordprocessingDocument.Open(ms, false);
			}

			var wordFreqs = new ConcurrentDictionary<string, int>(_StringComparer);
			var stopwords = await _CommonStopwordsTask.ConfigureAwait(false);
			var isw = _ignoreStopwords;
			var asw = _additionalStopwords;
			if (isw.Any(sw => stopwords.Contains(sw)) || asw.Any(sw => !stopwords.Contains(sw)))
			{
				stopwords = new HashSet<string>(stopwords);
				if (isw != null)
					stopwords.ExceptWith(isw);
				if (asw != null)
					stopwords.UnionWith(asw);
			}

			var paras = doc.MainDocumentPart.Document.Body.Descendants<Paragraph>();
			foreach (var p in paras)
			{
				var paraText = string.Concat(from t in p.Descendants<Text>() select t.Text);
				for (int cx = 0; cx < paraText.Length; cx++)
				{
					// Look for start of a word. We consider apostrophes to be word characters, but will remove matching pairs at start/end.
					if (paraText[cx] == '\'' || char.IsLetter(paraText[cx]))
					{
						// Record word start
						var ws = cx++;
						// Look for rest of word.
						while (cx < paraText.Length && (paraText[cx] == '\'' || char.IsLetter(paraText[cx])))
							cx++;

						// Trim matched apostrophes around word
						int wl = cx - ws;
						while (wl > 1 && paraText[ws] == '\'' && paraText[ws + wl - 1] == '\'')
						{
							ws++;
							wl -= 2;
						}

						// Count word, unless it was a stop word
						if (wl > 1)
						{
							var word = paraText.Substring(ws, wl);
							var stemmed = _Stemmer.Stem(word);
							if (!(await _CommonStopwordsTask).Contains(stemmed))
								wordFreqs.AddOrUpdate(word, w => 1, (w, c) => c + 1);
						}
					}
				}
			}

			return wordFreqs;
		}

		static HashSet<string> ParseStopwordList(string plaintext)
		{
			return new HashSet<string>(from word in plaintext.Split((char[])null, StringSplitOptions.RemoveEmptyEntries) select _Stemmer.Stem(word), _StringComparer);
		}

		async void tbAddStopwords_OnTextChanged(object sender, TextChangedEventArgs e)
		{
			_additionalStopwords = ParseStopwordList(tbAddStopwords.Text);
			await RenderImage();
		}

		async void tbRemoveStopwords_OnTextChanged(object sender, TextChangedEventArgs e)
		{
			_ignoreStopwords = ParseStopwordList(tbRemoveStopwords.Text);
			await RenderImage();
		}

		async void Window_Loaded(object sender, RoutedEventArgs e)
		{
			tbCommonStopwords.Text = string.Join(Environment.NewLine, await _CommonStopwordsTask);
		}

		void Choose_OnClick(object sender, RoutedEventArgs e)
		{
			throw new NotImplementedException();
		}

		async void CbStemOnChecked(object sender, RoutedEventArgs e)
		{
			await RenderImage();
		}

		async void AnalyzeClick(object sender, RoutedEventArgs e)
		{
			// TODO
		}
	}
}
