namespace Sefic.Application.Common;

public class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("Um ou mais campos são inválidos.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public class NotFoundException(string message) : Exception(message);

public class ConflictException(string message) : Exception(message);
