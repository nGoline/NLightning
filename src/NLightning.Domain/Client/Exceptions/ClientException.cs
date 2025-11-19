namespace NLightning.Domain.Client.Exceptions;

public class ClientException : Exception
{
    public string ErrorCode { get; set; }

    public ClientException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    public ClientException(string errorCode, string message, Exception innerException) : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}