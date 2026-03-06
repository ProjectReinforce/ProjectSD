namespace Shared.Kernel
{
    public sealed class Result
    {
        private Result(bool isSuccess, string error)
        {
            IsSuccess = isSuccess;
            Error = error ?? string.Empty;
        }

        public bool IsSuccess { get; }
        public bool IsFailure
        {
            get { return !IsSuccess; }
        }

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

    public sealed class Result<T>
    {
        private Result(T value, bool isSuccess, string error)
        {
            Value = value;
            IsSuccess = isSuccess;
            Error = error ?? string.Empty;
        }

        public T Value { get; }
        public bool IsSuccess { get; }
        public bool IsFailure
        {
            get { return !IsSuccess; }
        }

        public string Error { get; }

        public static Result<T> Success(T value)
        {
            return new Result<T>(value, true, string.Empty);
        }

        public static Result<T> Failure(string error)
        {
            return new Result<T>(
                default(T),
                false,
                string.IsNullOrWhiteSpace(error) ? "Unknown error." : error.Trim());
        }
    }
}
