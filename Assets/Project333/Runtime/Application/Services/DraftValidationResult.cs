namespace Project333.Runtime.Application.Services
{
    public sealed class DraftValidationResult
    {
        private DraftValidationResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message ?? string.Empty;
        }

        public bool IsValid { get; }

        public string Message { get; }

        public static DraftValidationResult Success()
        {
            return new DraftValidationResult(true, string.Empty);
        }

        public static DraftValidationResult Failure(string message)
        {
            return new DraftValidationResult(false, message);
        }
    }
}
