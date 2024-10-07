using System.Collections;

namespace BlindCatMaui.SDControls;

public class TagsLayout : FlexLayout, IDisposable
{
    private readonly List<View> _cache = new();

    // items solurce
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(IList),
        typeof(TagsLayout),
        null,
        propertyChanged: (b,o,n) =>
        {
            if (b is TagsLayout self)
                self.Update();
        }
    );
    public IList? ItemsSource
    {
        get => GetValue(ItemsSourceProperty) as IList;
        set => SetValue(ItemsSourceProperty, value);
    }

    // item template
    public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(
        nameof(ItemTemplate),
        typeof(DataTemplate),
        typeof(TagsLayout),
        null,
        propertyChanged: (b, o, n) =>
        {
            if (b is TagsLayout self)
            {
                self.Update();
            }
        }
    );
    public DataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty) as DataTemplate;
        set => SetValue(ItemTemplateProperty, value);
    }

    private void Update()
    {
        int old = Children.Count;
        int nev = (ItemTemplate != null) ? (ItemsSource?.Count ?? 0) : 0;
        int max = Math.Max(old, nev);

        for (int i = 0; i < max; i++)
        {
            View v;

            // del
            if (i > nev - 1)
            {
                var del = (View)Children.Last();
                Children.Remove(del);
                _cache.Add(del);
                del.BindingContext = null;
            }
            else
            {
                // current
                if (i <= old - 1)
                {
                    v = (View)Children[i];
                    v.BindingContext = ItemsSource![i];
                }
                // need new
                else if (_cache.Count == 0)
                {
                    v = (View)ItemTemplate!.CreateContent();
                    v.BindingContext = ItemsSource![i];
                    Children.Add(v);
                }
                else
                {
                    v = _cache.Last();
                    _cache.Remove(v);
                    v.BindingContext = ItemsSource![i];
                }
            }
        }
    }

    public void Dispose()
    {
        _cache.Clear();
        GC.SuppressFinalize(this);
    }
}