namespace Next.Common
{
    // ────────────────────────────────────────────────────────────────
    // PUBLIC INTERFACES (visible to library consumers)
    // ────────────────────────────────────────────────────────────────

    public interface IResult
    {
        bool IsOk { get; }
        string Message { get; }
    }

    public interface IResult<out T> : IResult
    {
        T Data { get; }
    }

    public interface IResponse
    {
        bool Success { get; }
        int StatusCode { get; }
        string Body { get; }
        string ErrorMessage { get; }
    }

    public interface IResponse<out T> : IResponse
    {
        T Data { get; }
    }

    // ────────────────────────────────────────────────────────────────
    // INTERNAL IMPLEMENTATIONS (hidden from outside the assembly)
    // ────────────────────────────────────────────────────────────────

    internal abstract class Result : IResult
    {
        public bool IsOk { get; private set; }
        public string Message { get; private set; }

        protected Result(bool isOk, string message)
        {
            IsOk = isOk;
            Message = message  ?? string.Empty;
        }
    }

    internal sealed class SuccessResult : Result
    {
        internal SuccessResult(string message) : base(true, message) { }
    }

    internal sealed class FailureResult : Result
    {
        internal FailureResult(string message) : base(false, message) { }
    }

    internal abstract class Result<T> : Result, IResult<T>
    {
        public T Data { get; private set; }

        protected Result(bool isOk, string message, T data)
            : base(isOk, message)
        {
            Data = data;
        }
    }

    internal sealed class SuccessResult<T> : Result<T>
    {
        internal SuccessResult(T data, string message)
            : base(true, message, data) { }
    }

    internal sealed class FailureResult<T> : Result<T>
    {
        internal FailureResult(string message)
            : base(false, message, default) { }
    }

    internal class ApiResponse : IResponse
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string Body { get; set; }
        public string ErrorMessage { get; set; }
    }

    internal sealed class ApiResponse<T> : ApiResponse, IResponse<T>
    {
        public T Data { get; set; }
    }

    // ────────────────────────────────────────────────────────────────
    // PUBLIC FACTORIES (the only way consumers should create results)
    // ────────────────────────────────────────────────────────────────

    public static class Results
    {
        public static IResult Ok(string message = "Success")
            => new SuccessResult(message);

        public static IResult Error(string message)
            => new FailureResult(message);

        public static IResult<T> Ok<T>(T data, string message = "Success")
            => new SuccessResult<T>(data, message);

        public static IResult<T> Error<T>(string message)
            => new FailureResult<T>(message);
    }

    public static class ApiResponses
    {
        public static IResponse Ok(int statusCode = 200, string body = null)
            => new ApiResponse
            {
                Success = true,
                StatusCode = statusCode,
                Body = body
            };

        public static IResponse Error(int statusCode, string errorMessage, string body = null)
            => new ApiResponse
            {
                Success = false,
                StatusCode = statusCode,
                ErrorMessage = errorMessage,
                Body = body
            };

        public static IResponse<T> Ok<T>(T data, int statusCode = 200, string body = null)
            => new ApiResponse<T>
            {
                Success = true,
                StatusCode = statusCode,
                Body = body,
                Data = data
            };

        public static IResponse<T> Error<T>(string errorMessage, int statusCode = 400, string body = null)
            => new ApiResponse<T>
            {
                Success = false,
                StatusCode = statusCode,
                ErrorMessage = errorMessage,
                Body = body
            };
    }
}