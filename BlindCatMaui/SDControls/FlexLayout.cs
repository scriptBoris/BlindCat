using Microsoft.Maui.Layouts;
using System.Collections;
using System.Collections.Specialized;

namespace BlindCatMaui.SDControls;

public class FlexLayout : Layout, ILayoutManager
{
    #region bindable props
    // spacing
    public static readonly BindableProperty SpacingProperty = BindableProperty.Create(
        nameof(Spacing),
        typeof(double),
        typeof(FlexLayout),
        10.0,
        propertyChanged: (b, o, n) =>
        {
            if (b is FlexLayout self)
                self.InvalidateMeasure();
        }
    );
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    //// items source
    //public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
    //    nameof(ItemsSource),
    //    typeof(IList),
    //    typeof(FlexLayout),
    //    null,
    //    propertyChanged: (b, o, n) =>
    //    {
    //        if (b is not FlexLayout self)
    //            return;

    //        if (o is INotifyCollectionChanged oc)
    //            oc.CollectionChanged -= self.Nc_CollectionChanged;

    //        if (n is INotifyCollectionChanged nc)
    //            nc.CollectionChanged += self.Nc_CollectionChanged;

    //        self.UpdateItems();
    //    }
    //);

    //private void Nc_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    //{
    //    UpdateItems();
    //}

    //public IList? ItemsSource
    //{
    //    get => GetValue(ItemsSourceProperty) as IList;
    //    set => SetValue(ItemsSourceProperty, value);
    //}

    //// item template
    //public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(
    //    nameof(ItemTemplate),
    //    typeof(DataTemplate),
    //    typeof(FlexLayout),
    //    null,
    //    propertyChanged: (b, o, n) =>
    //    {
    //        if (b is FlexLayout self)
    //        {
    //            self.UpdateItems();
    //            self.InvalidateMeasure();
    //        }
    //    }
    //);
    //public DataTemplate? ItemTemplate
    //{
    //    get => GetValue(ItemTemplateProperty) as DataTemplate;
    //    set => SetValue(ItemTemplateProperty, value);
    //}
    #endregion bindable props

    //private void UpdateItems()
    //{
    //    Children.Clear();

    //    if (ItemsSource == null)
    //        return;

    //    foreach (var item in ItemsSource)
    //    {
    //        View view;

    //        if (item is View itemView)
    //        {
    //            view = itemView;
    //            view.BindingContext = this.BindingContext;
    //        }
    //        else
    //        {
    //            if (ItemTemplate?.CreateContent() is not View vv)
    //                return;

    //            view = vv;
    //            view.BindingContext = item;
    //        }

    //        Children.Add(view);
    //    }
    //}

    public Size ArrangeChildren(Rect bounds)
    {
        double viewPortWidth = bounds.Width;
        if (Children.Count == 0)
            return Size.Zero;

        double usedHeight = 0;
        double currentRowHeight = -1;
        double currentRowFreeWidth = viewPortWidth;
        double offsetX = 0;

        for (int i = 0; i < Children.Count; i++)
        {
            var item = (View)Children[i];
            var iview = (IView)item;
            if (!item.IsVisible)
                continue;

            var m = iview.DesiredSize;

            // помещается в строку
            if (m.Width <= currentRowFreeWidth)
            {
                if (currentRowHeight < m.Height)
                    currentRowHeight = m.Height;

                double w = m.Width;
                var rect = new Rect
                {
                    X = offsetX,
                    Y = usedHeight,
                    Width = w,
                    Height = m.Height,
                };
                iview.Arrange(rect);

                offsetX += m.Width + Spacing;
                currentRowFreeWidth -= Spacing + w;
            }
            // на новую строку
            else
            {
                double w = (m.Width > viewPortWidth) ? viewPortWidth : m.Width;

                usedHeight += currentRowHeight + Spacing;
                currentRowHeight = m.Height;
                currentRowFreeWidth = viewPortWidth - w;
                offsetX = w + Spacing;

                var rect = new Rect
                {
                    X = 0,
                    Y = usedHeight,
                    Width = w,
                    Height = m.Height,
                };
                iview.Arrange(rect);
            }
        }

        return bounds.Size;
    }

    public Size Measure(double widthConstraint, double heightConstraint)
    {
        // todo наверное сделать для горизонтального лайна
        //if (double.IsInfinity(widthConstraint))
        //{
        //}

        double viewPortWidth = double.IsInfinity(widthConstraint) ? 10000 : widthConstraint;
        if (Children.Count == 0)
            return Size.Zero;

        double usedHeight = 0;
        double currentRowHeight = -1;
        double currentRowFreeWidth = viewPortWidth;

        for (int i = 0; i < Children.Count; i++)
        {
            var item = (View)Children[i];
            var iview = (IView)item;
            if (!item.IsVisible)
                continue;

            var m = iview.Measure(viewPortWidth, double.PositiveInfinity);

            // помещается в строку
            if (m.Width <= currentRowFreeWidth)
            {
                if (currentRowHeight < m.Height)
                    currentRowHeight = m.Height;

                currentRowFreeWidth -= Spacing + m.Width;
            }
            // на новую строку
            else
            {
                double w = (m.Width > viewPortWidth) ? viewPortWidth : m.Width;
                usedHeight += currentRowHeight + Spacing;
                currentRowHeight = m.Height;
                currentRowFreeWidth = viewPortWidth - w;
            }
        }

        usedHeight = usedHeight + currentRowHeight;

        return new Size(viewPortWidth, usedHeight);
    }

    protected override ILayoutManager CreateLayoutManager()
    {
        return this;
    }
}