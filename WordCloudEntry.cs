using System;

namespace KeywordDensity
{
	public class WordCloudEntry
	{
		public string Word { get; set; }
		public string Stem { get; set; }
		public int Count { get; set; }
		public bool Highlight { get; set; }

		public static readonly Func<WordCloudEntry, string> WordGetter = e => e.Word;
		public static readonly Func<WordCloudEntry, string> StemGetter = e => e.Stem;
	}
}
