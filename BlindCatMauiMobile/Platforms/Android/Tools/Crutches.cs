using System.Diagnostics;
using Android.Content;
using Android.Database;
using Android.OS;
using Android.Provider;
using Application = Android.App.Application;
using Debug = System.Diagnostics.Debug;

namespace BlindCatMauiMobile.Tools;

public static class Crutches
{
    // private static Context _context => MainActivity.Instance;
    private static Context _context => Application.Context;

    public static string GetActualPathFromFile(Android.Net.Uri uri)
    {
        bool isKitKat = Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Kitkat;

        if (isKitKat && DocumentsContract.IsDocumentUri(_context, uri))
        {
            // ExternalStorageProvider
            if (isExternalStorageDocument(uri))
            {
                string docId = DocumentsContract.GetDocumentId(uri);

                char[] chars = { ':' };
                string[] split = docId.Split(chars);
                string type = split[0];

                if ("primary".Equals(type, StringComparison.OrdinalIgnoreCase))
                {
                    return Android.OS.Environment.ExternalStorageDirectory + "/" + split[1];
                }
            }
            // DownloadsProvider
            else if (isDownloadsDocument(uri))
            {
                string id = DocumentsContract.GetDocumentId(uri);
                string path = "";

                //Starting with Android O, this "id" is not necessarily a long (row number),
                //but might also be a "raw:/some/file/path" URL
                if (id != null && id.StartsWith("raw:/"))
                {
                    var rawuri = Android.Net.Uri.Parse(id);
                    path = rawuri.Path;
                }
                else
                {
                    long longId;
                    string[] parts = id.Split(':');
                    if (parts.Length < 2)
                    {
                        longId = long.Parse(id);
                    }

                    if (parts.Length == 2)
                    {
                        longId = long.Parse(parts[1]);
                    }
                    else
                    {
                        Debugger.Break();
                        throw new InvalidOperationException();
                    }

                    var uris = new Android.Net.Uri[]
                    {
                        MediaStore.Downloads.ExternalContentUri.WithId(longId),
                        MediaStore.Downloads.InternalContentUri.WithId(longId),
                    };

                    foreach (var u in uris)
                    {
                        path = GetDataColumn1(_context, u, null, null);

                        if (!string.IsNullOrEmpty(path))
                            break;
                    }
                }
                return path;
            }
            // MediaProvider
            else if (isMediaDocument(uri))
            {
                string docId = DocumentsContract.GetDocumentId(uri);

                char[] chars = { ':' };
                string[] split = docId.Split(chars);

                string type = split[0];

                Android.Net.Uri contentUri = null;
                if ("image".Equals(type))
                {
                    contentUri = MediaStore.Images.Media.ExternalContentUri;
                }
                else if ("video".Equals(type))
                {
                    contentUri = MediaStore.Video.Media.ExternalContentUri;
                }
                else if ("audio".Equals(type))
                {
                    contentUri = MediaStore.Audio.Media.ExternalContentUri;
                }

                String selection = "_id=?";
                String[] selectionArgs = new String[]
                {
                    split[1]
                };

                return GetDataColumn(_context, contentUri, selection, selectionArgs);
            }
        }
        // MediaStore (and general)
        else if ("content".Equals(uri.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            // Return the remote address
            if (isGooglePhotosUri(uri))
                return uri.LastPathSegment;

            return GetDataColumn(_context, uri, null, null);
        }
        // File
        else if ("file".Equals(uri.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return uri.Path;
        }

        return null;
    }

    private static string? GetDataColumn1(Context context, Android.Net.Uri uri, string? selection,
        string[]? selectionArgs)
    {
        ICursor? cursor = null;
        string? result = null;
        string column = "_data";
        string[] projection =
        {
            // MediaStore.MediaColumns.DisplayName,
            column,
        };

        try
        {
            cursor = context
                .ContentResolver?
                .Query(uri,
                    projection,
                    selection,
                    selectionArgs,
                    null
                );

            if (cursor != null && cursor.MoveToFirst())
            {
                int index = cursor.GetColumnIndexOrThrow(column);
                result = cursor.GetString(index);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return null;
        }
        finally
        {
            cursor?.Close();
        }

        return result;
    }

    private static string? GetDataColumn(Context context, Android.Net.Uri uri, string? selection,
        string[]? selectionArgs)
    {
        ICursor? cursor = null;
        string? result = null;
        string column = "_data";

        string[] projection =
        {
            "media-database-columns-to-retrieve",
        };

        try
        {
            cursor = context
                .ContentResolver?
                .Query(
                    MediaStore.Audio.Media.ExternalContentUri,
                    ["media-database-columns-to-retrieve"],
                    "sql-where-clause-with-placeholder-variables",
                    ["values-of-placeholder-variables"],
                    "sql-order-by-clause"
                );

            if (cursor != null && cursor.MoveToFirst())
            {
                int index = cursor.GetColumnIndexOrThrow(column);
                result = cursor.GetString(index);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return null;
        }
        finally
        {
            cursor?.Close();
        }

        return result;
    }

    //Whether the Uri authority is ExternalStorageProvider.
    private static bool isExternalStorageDocument(Android.Net.Uri uri)
    {
        return "com.android.externalstorage.documents".Equals(uri.Authority);
    }

    //Whether the Uri authority is DownloadsProvider.
    private static bool isDownloadsDocument(Android.Net.Uri uri)
    {
        return "com.android.providers.downloads.documents".Equals(uri.Authority);
    }

    //Whether the Uri authority is MediaProvider.
    private static bool isMediaDocument(Android.Net.Uri uri)
    {
        return "com.android.providers.media.documents".Equals(uri.Authority);
    }

    //Whether the Uri authority is Google Photos.
    private static bool isGooglePhotosUri(Android.Net.Uri uri)
    {
        return "com.google.android.apps.photos.content".Equals(uri.Authority);
    }

    public static Android.Net.Uri WithId(this Android.Net.Uri self, long id)
    {
        return ContentUris.WithAppendedId(self, id);
    }
}