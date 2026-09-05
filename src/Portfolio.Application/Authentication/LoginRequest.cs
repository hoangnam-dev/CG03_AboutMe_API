using System.ComponentModel.DataAnnotations;

namespace Portfolio.Application.Authentication;

public sealed record LoginRequest(
    [Required, EmailAddress, MaxLength(320)] string Email,
    [Required, MaxLength(256)] string Password);
