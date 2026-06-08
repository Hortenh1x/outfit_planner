namespace OutfitPlanner.Application.Abstractions;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string passwordHash, string password);
}

public interface IAuthTokenService
{
    string CreateToken();
    string HashToken(string token);
}
