namespace DentalLab.Application.Models;

public sealed class UserOperationResult
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public static UserOperationResult Success()
    {
        return new UserOperationResult
        {
            Succeeded = true
        };
    }

    public static UserOperationResult Failure(string errorMessage)
    {
        return new UserOperationResult
        {
            Succeeded = false,
            ErrorMessage = errorMessage
        };
    }
}