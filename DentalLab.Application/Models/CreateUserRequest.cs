using System.ComponentModel.DataAnnotations;
namespace DentalLab.Application.Models;
public sealed class CreateUserRequest
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Required] public string Role { get; set; } = string.Empty;
}
