using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SabakiumAPI.Models;

namespace SabakiumAPI.Services;

public class AuthService (IConfiguration config)
{
    private readonly string _secret = config["Jwt:Secret"]
                                      ?? throw new InvalidOperationException("Jwt:Secret not configured");
 
    public string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
 
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("displayName", user.DisplayName),
        };
 
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds
        );
 
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}