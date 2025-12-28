
namespace Next.Common
{
    public class Return
    {
        public bool IsOk { get; private set; }
        public string Message { get; private set; }

        protected Return(bool success, string message)
        {
            IsOk = success;
            Message = message;
        }

        public static Return Ok(string message = "Success") => new Return(true, message);
        public static Return Error(string message) => new Return(false, message);
    }

    public class Return<T> : Return
    {
        public T Data { get; private set; }

        private Return(bool success, string message, T data = default) : base(success, message)
        {
            Data = data;
        }

        public static Return<T> Ok(T data, string message = "Success") => new Return<T>(true, message, data);
        public new static Return<T> Error(string message) => new Return<T>(false, message);
    }

    public class Response
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string Body { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class Response<T> : Response
    {
        public T Data { get; set; }
    }
}
