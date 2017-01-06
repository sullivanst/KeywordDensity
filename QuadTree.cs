using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace KeywordDensity
{
	public class QuadTree<T>
	{
		const int EntriesBeforeSplit = 10;

		public struct Entry
		{
			public Rect Bounds { get; set; }
			public T Item { get; set; }
		}

		Rect _bounds;
		readonly List<Entry> _entries = new List<Entry>();
		readonly Func<T, Rect> _boundsGetter;
		readonly bool _isRoot;

		QuadTree<T>[] _quadrants;

		public QuadTree(Rect bounds, Func<T, Rect> boundsGetter) : this(bounds, boundsGetter, true) { }

		QuadTree(Rect bounds, Func<T, Rect> boundsGetter, bool isRoot)
		{
			_bounds = bounds;
			_boundsGetter = boundsGetter;
			_isRoot = isRoot;
		}


		QuadTree(Rect bounds, Func<T, Rect> boundsGetter, List<Entry> candidates) : this(bounds, boundsGetter, false)
		{
			for (int i = candidates.Count - 1; i >= 0; i--)
			{
				var entry = candidates[i];
				if (_bounds.Contains(entry.Bounds))
				{
					_entries.Add(entry);
					candidates.RemoveAt(i);
				}
			}
		}

		void Split()
		{
			if (null != _quadrants)
				throw new InvalidOperationException("Already split");

			var halfWidth = _bounds.Width/2;
			var halfHeight = _bounds.Height/2;
			var midX = _bounds.X + halfWidth;
			var midY = _bounds.Y + halfHeight;

			_quadrants = new []
			{
				/* NW */ new QuadTree<T>(new Rect(_bounds.X, _bounds.Y, halfWidth, halfHeight), _boundsGetter, _entries),
				/* NE */ new QuadTree<T>(new Rect(midX, _bounds.Y, halfWidth, halfHeight), _boundsGetter, _entries),
				/* SW */ new QuadTree<T>(new Rect(_bounds.X, midY, halfWidth, halfHeight), _boundsGetter, _entries),
				/* SE */ new QuadTree<T>(new Rect(midX, midY, halfWidth, halfHeight), _boundsGetter, _entries)
			};
		}

		public void Add(T item) => Add(new Entry { Bounds = _boundsGetter(item), Item = item });

		public void Add(Entry entry)
		{
			if (_quadrants == null && _entries.Count >= EntriesBeforeSplit)
				Split();

			if (!_bounds.Contains(entry.Bounds))
			{
				if (!_isRoot)
					throw new InvalidOperationException("Should never attempt to add uncontained item to non-root node");

				var allEntries = AllEntries.ToList();
				Clear();
				_bounds = Rect.Union(_bounds, entry.Bounds);
				foreach (var e in allEntries)
					Add(e); // NOTE: should never recurse
			}

			if (null != _quadrants)
			{
				var midX = _bounds.X + _bounds.Width/2;
				var midY = _bounds.Y + _bounds.Height/2;


				var isNorth = entry.Bounds.Bottom < midY;
				var isSouth = !isNorth && entry.Bounds.Top >= midY;
				if (isNorth || isSouth)
				{
					var isWest = entry.Bounds.Right < midX;
					var isEast = !isWest && entry.Bounds.Left >= midX;

					if (isWest || isEast)
					{
						_quadrants[(isNorth ? 0 : 2) + (isWest ? 0 : 1)].Add(entry);
						return;
					}
				}

				_entries.Add(entry);
			}
		}

		public bool HasCollision(Rect rect)
				=>
				_bounds.IntersectsWith(rect) &&
				(_entries.Exists(e => e.Bounds.IntersectsWith(rect)) || (_quadrants != null && _quadrants.Any(t => t.HasCollision(rect))));

		public bool HasCollision(T item) => HasCollision(_boundsGetter(item));

		public bool HasCollision(Entry entry) => HasCollision(entry.Bounds);

		public IEnumerable<Entry> GetEntriesContaining(Point point)
		{
			if (!_bounds.Contains(point))
				return Enumerable.Empty<Entry>();

			var matches = _entries.Where(e => e.Bounds.Contains(point));
			if (null == _quadrants)
				return matches;

			var isNorth = point.Y < _bounds.Y + _bounds.Height/2;
			var isWest = point.X < _bounds.X + _bounds.Width/2;
			return matches.Concat(_quadrants[(isNorth ? 0 : 2) + (isWest ? 0 : 1)].GetEntriesContaining(point));
		}

		public IEnumerable<T> GetItemsContaining(Point point) => GetEntriesContaining(point).Select(e => e.Item);

		public void Clear()
		{
			_entries.Clear();
			_quadrants = null;
		}

		public IEnumerable<Entry> AllEntries => null == _quadrants ? _entries : _entries.Concat(_quadrants.SelectMany(q => q.AllEntries));

		public IEnumerable<T> AllItems => AllEntries.Select(e => e.Item);
	}
}
