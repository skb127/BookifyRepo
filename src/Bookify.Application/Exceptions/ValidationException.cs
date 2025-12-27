namespace Bookify.Application.Exceptions;

public sealed class ValidationException : Exception
{
    public ValidationException()
    {
    }
    
    public ValidationException(string message)
        : base(message)
    {
    }
    
    public ValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
    
    public ValidationException(IEnumerable<ValidationError> errors) => Errors = errors;

    public IEnumerable<ValidationError> Errors { get; } = [];
}
