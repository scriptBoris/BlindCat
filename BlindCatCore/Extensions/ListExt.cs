using System.Collections.ObjectModel;

namespace BlindCatCore.Extensions;

public static class ListExt
{
    public static async Task<ObservableCollection<T>> ToObs<T>(this Task<T[]> collection)
    {
        var res = await collection;
        return new ObservableCollection<T>(res);
    }

    public static ObservableCollection<T> ToObs<T>(this IEnumerable<T> collection)
    {
        return new ObservableCollection<T>(collection);
    }
}