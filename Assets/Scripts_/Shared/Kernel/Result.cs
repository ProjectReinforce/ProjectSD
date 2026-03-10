namespace Shared.Kernel
{
    public readonly struct Result
    {
        private Result(bool isSuccess, string error)
        {
            IsSuccess = isSuccess;
            Error = error ?? string.Empty;
        }

        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public string Error { get; }

        public static Result Success()
        {
            return new Result(true, string.Empty);
        }

        public static Result Failure(string error)
        {
            return new Result(false, string.IsNullOrWhiteSpace(error) ? "Unknown error." : error.Trim());
        }
    }

    public readonly struct Result<T>
    {
        private Result(T value, bool isSuccess, string error)
        {
            Value = value;
            IsSuccess = isSuccess;
            Error = error ?? string.Empty;
        }

        public T Value { get; }
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public string Error { get; }

        public static Result<T> Success(T value)
        {
            return new Result<T>(value, true, string.Empty);
        }

        public static Result<T> Failure(string error)
        {
            return new Result<T>(
                default,
                false,
                string.IsNullOrWhiteSpace(error) ? "Unknown error." : error.Trim());
        }
    }
}
