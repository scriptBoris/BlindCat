namespace BlindCatCore.Core;


public class AppResponseError : AppResponse
{
}

public class AppResponse<T> : AppResponse, IDisposable
{
    public T Result { get; set; } = default!;

    public static implicit operator AppResponse<T>(AppResponseError error) => new()
    {
        Code = error.Code,
        Description = error.Description,
        Exception = error.Exception,
        ValidationErrors = error.ValidationErrors,
        Result = default!,
        IsCanceled = error.IsCanceled,
    };

    public AppResponse<T2> ReplaceResult<T2>(T2 result)
        where T2 : class
    {
        return new AppResponse<T2>
        {
            Code = Code,
            Description = Description,
            Result = result,
            Exception = Exception,
            ValidationErrors = ValidationErrors,
            IsCanceled = IsCanceled,
        };
    }

    public void Dispose()
    {
        if (Result is IDisposable disposable)
            disposable.Dispose();
    }
}

public class AppResponse
{
    public required int Code { get; set; }
    public required string Description { get; set; }

    public Dictionary<string, string[]> ValidationErrors { get; set; } = null!;

    public Exception? Exception { get; protected set; }

    public bool IsCanceled { get; protected set; }

    public bool IsSuccess => Code == 0;

    public bool IsFault => !IsSuccess;

    public string MessageForLog 
    {
        get
        {
            string desc = Description ?? "<NO_DESCRIPTION>";
            if (desc.Length > 20)
            {
                desc = $"\n{desc}";
            }

            string? exception = Exception?.ToString();
            if (exception != null)
            {
                if (exception.Length > 20)
                    exception = $"\nException:\n{exception}";
                else
                    exception = $"\nException: {exception}";
            }


            return 
                $"Code: {Code}\n" +
                $"Description: {desc}{exception}";
        }
    }

    public static AppResponse OK => new AppResponse
    {
        Code = 0,
        Description = "Success"
    };

    public static AppResponseError Canceled => new AppResponseError
    {
        Code = -900,
        IsCanceled = true,
        Description = "Canceled by client"
    };

    public AppResponseError AsError =>
        new AppResponseError
        {
            Code = Code,
            Description = Description,
            ValidationErrors = ValidationErrors,
            Exception = Exception,
            IsCanceled = IsCanceled,
        };

    public static AppResponse<T> Result<T>(T result)
    {
        return new AppResponse<T>
        {
            Code = 0,
            Result = result,
            Description = "Success",
        };
    }

    public static AppResponseError Error(string message)
    {
        return new AppResponseError
        {
            Code = -1,
            Description = message,
        };
    }

    public static AppResponseError Error(string message, int code)
    {
        return new AppResponseError
        {
            Code = code,
            Description = message,
        };
    }

    public static AppResponseError Error(string message, int code, Exception? ex)
    {
        return new AppResponseError
        {
            Code = code,
            Description = message,
            Exception = ex
        };
    }
}
