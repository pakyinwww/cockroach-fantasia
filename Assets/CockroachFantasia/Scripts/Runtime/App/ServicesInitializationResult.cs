namespace CockroachFantasia.App
{
    public readonly struct ServicesInitializationResult
    {
        public ServicesInitializationResult(bool succeeded, ServicesErrorKind errorKind, string message)
        {
            Succeeded = succeeded;
            ErrorKind = errorKind;
            Message = message;
        }

        public bool Succeeded { get; }
        public ServicesErrorKind ErrorKind { get; }
        public string Message { get; }

        public static ServicesInitializationResult Success(string message = "Online services are ready.")
        {
            return new ServicesInitializationResult(true, ServicesErrorKind.None, message);
        }

        public static ServicesInitializationResult Failure(ServicesErrorKind kind, string message)
        {
            return new ServicesInitializationResult(false, kind, message);
        }
    }
}
