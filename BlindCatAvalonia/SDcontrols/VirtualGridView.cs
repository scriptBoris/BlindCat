using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.VisualTree;

namespace BlindCatAvalonia.SDcontrols;

public interface IVirtualGridRecycle
{
    bool IsVirtualAttach { get; set; }
    bool IsVirtualDeattach { get; set; }
}

public class VirtualGridView : ScrollViewer
{
    private readonly VirtualGrid _grid;

    public VirtualGridView()
    {
        _grid = new VirtualGrid(this);
        Content = _grid;
    }

    #region bindable props
    // items source
    public static readonly StyledProperty<IList?> ItemsSourceProperty = AvaloniaProperty.Register<VirtualGridView, IList?>(
        nameof(ItemsSource)
    );
    public IList? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    // item template
    public static readonly StyledProperty<DataTemplate?> ItemTemplateProperty = AvaloniaProperty.Register<VirtualGridView, DataTemplate?>(
        nameof(ItemTemplate)
    );
    public DataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    // width
    public static readonly StyledProperty<double> ItemWidthProperty = AvaloniaProperty.Register<VirtualGridView, double>(
        nameof(ItemWidth),
        250.0
    );
    public double ItemWidth
    {
        get => GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    // height
    public static readonly StyledProperty<double> ItemHeightProperty = AvaloniaProperty.Register<VirtualGridView, double>(
        nameof(ItemHeight),
        250.0
    );
    public double ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }
    #endregion bindable props

    protected override Type StyleKeyOverride => typeof(ScrollViewer);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            _grid._cachePool.ClearContext();
            _grid.InvalidateMeasure();

            if (change.OldValue is INotifyCollectionChanged old)
                old.CollectionChanged -= _grid.Nev_CollectionChanged;

            if (change.NewValue is INotifyCollectionChanged nev)
                nev.CollectionChanged += _grid.Nev_CollectionChanged;
        }
        else if (change.Property == ItemTemplateProperty)
        {
            _grid.InvalidateMeasure();
        }
    }

    private class VirtualGrid : Panel
    {
        private readonly VirtualGridView _parent;
        public readonly CachePool _cachePool;
        private int totalCols;
        private int totalRows;

        private double realItemHeight;
        private double realItemWidth;

        public VirtualGrid(VirtualGridView parent)
        {
            _parent = parent;
            _cachePool = new CachePool(Children);
        }

        public void Nev_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    break;
                case NotifyCollectionChangedAction.Remove:
                    int delId = e.OldStartingIndex;
                    _cachePool.Deatach(delId, _parent.ItemsSource);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    break;
                case NotifyCollectionChangedAction.Move:
                    break;
                case NotifyCollectionChangedAction.Reset:
                    _cachePool.ClearContext();
                    break;
                default:
                    break;
            }
            this.InvalidateMeasure();
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (finalSize.Width == 0 || finalSize.Height == 0)
                return finalSize;

            if (_parent.ItemsSource == null || _parent.ItemTemplate == null)
                return finalSize;

            int itemsCount = _parent.ItemsSource.Count;
            if (itemsCount == 0)
                return finalSize;

            double offsetY = _parent.Offset.Y;
            double offsetYEnd = offsetY + _parent.Viewport.Height;

            double d1 = offsetY / realItemHeight;
            int rowIdStart = (int)Math.Floor(d1);

            double d2 = offsetYEnd / realItemHeight;
            int rowIdEnd = (int)Math.Floor(d2);

            int visibleRows = rowIdEnd - rowIdStart + 1;

            double x = 0;
            double y = rowIdStart * realItemHeight;

            int i = 0;
            int itemId = rowIdStart * totalCols;
            int itemIdEnd = itemId + (visibleRows * totalCols - 1);

            _cachePool.SetScope(itemId, itemIdEnd);

            for (int rowId = rowIdStart; rowId <= rowIdEnd; rowId++)
            {
                for (int xOffset = 0; xOffset < totalCols; xOffset++, i++, itemId++)
                {
                    if (itemId > itemsCount - 1)
                        return finalSize;

                    object? context = _parent.ItemsSource[itemId];

                    var item = _cachePool.Fetch(itemId);
                    if (item == null)
                        item = _cachePool.Push(itemId, context, _parent.ItemTemplate);

                    // draw
                    var rect = new Rect(x, y, realItemWidth, realItemHeight);
                    item.Arrange(rect);
                    x += realItemWidth;
                }
                y += realItemHeight;
                x = 0;
            }

            return finalSize;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double w = availableSize.Width;
            double itemW = _parent.ItemWidth;
            double itemH = _parent.ItemHeight;
            int itemsCount = _parent.ItemsSource?.Count ?? 0;
            if (w == 0 || itemW == 0 || itemH == 0 || itemsCount == 0)
                return new Size(0, 0);

            double d1 = (w / itemW);
            int cols = (int)Math.Floor(d1);
            totalCols = cols;

            double d2 = (double)itemsCount / (double)cols;
            int rows = (int)Math.Ceiling(d2);
            totalRows = rows;

            realItemHeight = w / cols;
            realItemWidth = w / cols;

            double resultHeight = rows * realItemHeight;
            return new Size(w, resultHeight);
        }
    }

    private class VirtualItem
    {
        public int ItemId { get; set; }
        public required Control View { get; set; }
    }

    private class CachePool
    {
        private readonly List<VirtualItem> _attachedItems = new();
        private readonly List<VirtualItem> _cache = new();
        private readonly Controls _uiTree;

        public CachePool(Controls childrens)
        {
            _uiTree = childrens;
        }

        public Control? Fetch(int index)
        {
            var m = _attachedItems.FirstOrDefault(x => x.ItemId == index);
            if (m == null)
                return null;

            return m.View;
        }

        public Control Push(int index, object? context, DataTemplate dataTemplate)
        {
            VirtualItem pushed;
            var cache = TryGetCache(index);
            if (cache != null)
            {
                pushed = cache;
                pushed.ItemId = index;
                pushed.View.DataContext = context;
                _cache.Remove(cache);
                Add(pushed.View, pushed, isCache: true);
            }
            else
            {
                var item = dataTemplate.Build(context) ?? throw new NullReferenceException();
                item.DataContext = context;
                pushed = new VirtualItem
                {
                    ItemId = index,
                    View = item,
                };
                Add(pushed.View, pushed);
            }

            //_attachedItems.Add(pushed);
            //_uiTree.Add(pushed.View);
            return pushed.View;
        }

        public void SetScope(int itemIdStart, int itemIdEnd)
        {
            for (int i = _attachedItems.Count - 1; i >= 0; i--)
            {
                var item = _attachedItems[i];
                bool inRange = (itemIdStart <= item.ItemId && item.ItemId <= itemIdEnd);
                if (!inRange)
                {
                    bool useCache = _cache.Count < 15;
                    //_uiTree.Remove(item.View);
                    //_attachedItems.Remove(item);
                    Remove(item.View, item, useCache);

                    if (useCache)
                        _cache.Add(item);
                }
            }
        }

        public void Deatach(int index, IList source)
        {
            int visCount = _attachedItems.Count;
            var m = _attachedItems.FirstOrDefault(x => x.ItemId == index);
            if (m == null)
                return;

            bool isAlreadyDeleted = false;
            var sortedItems = _attachedItems.OrderBy(x => x.ItemId).ToList();
            int posStart = sortedItems.IndexOf(m);
            sortedItems.Remove(m);

            // delete latest items
            if (visCount > source.Count || index == source.Count)
            {
                //_uiTree.Remove(m.View);
                //_attachedItems.Remove(m);
                Remove(m.View, m, true);
                _cache.Add(m);
                isAlreadyDeleted = true;
            }

            //int posEnd = visCount - 2;
            //if (posStart > posEnd)
            //    return;

            int offset = 0;
            int itemId = 0;
            for (int i = posStart; i <= sortedItems.Count - 1; i++, offset++)
            {
                itemId = index + offset;
                sortedItems[i].ItemId = itemId;
            }
            m.ItemId = itemId + 1;

            if (m.ItemId <= source.Count - 1)
            {
                m.View.DataContext = source[m.ItemId];
            }
            else if (!isAlreadyDeleted)
            {
                //_uiTree.Remove(m.View);
                //_attachedItems.Remove(m);
                Remove(m.View, m, true);
                _cache.Add(m);
            }
        }

        public void ClearContext()
        {
            for (int i = _attachedItems.Count - 1; i >= 0; i--)
            {
                bool useCache = _cache.Count < 15;
                var item = _attachedItems[i];
                //_uiTree.Remove(item.View);
                //_attachedItems.Remove(item);
                Remove(item.View, item, useCache);

                if (useCache)
                    _cache.Add(item);
            }
        }

        private VirtualItem? TryGetCache(int index)
        {
            if (_cache.Count == 0)
                return null;

            if (_cache.Count == 1)
                return _cache[0];

            var cache = _cache.FirstOrDefault(x => x.ItemId == index);
            if (cache == null)
                cache = _cache.FirstOrDefault();

            return cache;
        }

        private void Add(Control uiView, VirtualItem item, bool isCache = false)
        {
            var recycle = uiView as IVirtualGridRecycle;
            if (recycle != null)
                recycle.IsVirtualAttach = isCache;

            _uiTree.Add(uiView);
            _attachedItems.Add(item);

            if (recycle != null)
                recycle.IsVirtualAttach = false;
        }

        private void Remove(Control uiView, VirtualItem item, bool isCache)
        {
            var recycle = uiView as IVirtualGridRecycle;
            if (recycle != null)
                recycle.IsVirtualDeattach = isCache;

            _uiTree.Remove(uiView);
            _attachedItems.Remove(item);

            if (recycle != null)
                recycle.IsVirtualDeattach = false;
        }
    }
}
