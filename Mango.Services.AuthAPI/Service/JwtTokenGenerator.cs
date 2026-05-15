using Mango.Services.AuthAPI.Models;
using Mango.Services.AuthAPI.Service.IService;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Mango.Services.AuthAPI.Service
{
    public class JwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly JwtOptions _jwtOptions;

        public JwtTokenGenerator(IOptions<JwtOptions> jwtOptions)
        {
            _jwtOptions = jwtOptions.Value;
        }

        /// <summary>
        /// Genera un token JWT firmado para un usuario específico.
        /// </summary>
        /// <param name="applicationUser">Objeto que contiene la información del usuario desde la base de datos.</param>
        /// <param name="roles">Lista de roles asignados al usuario.</param>
        /// <returns>Un string que representa el JWT codificado.</returns>
        public string GenerateToken(ApplicationUser applicationUser, IEnumerable<string> roles)
        {
            // Manejador para la creación del token
            var tokenHandler = new JwtSecurityTokenHandler();

            // Recuperamos la clave secreta desde la configuración (inyectada vía IOptions)
            var key = Encoding.ASCII.GetBytes(_jwtOptions.Secret);

            // Definición de la carga útil (Payload)
            var claimList = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Email, applicationUser.Email),
                new Claim(JwtRegisteredClaimNames.Sub, applicationUser.Id),
                new Claim(JwtRegisteredClaimNames.Name, applicationUser.UserName)
            };

            // Agregar roles como claims
            claimList.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            // Configuración completa del token (Encabezado y Cuerpo)
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = _jwtOptions.Issuer,
                Audience = _jwtOptions.Audience,
                Subject = new ClaimsIdentity(claimList),
                Expires = DateTime.UtcNow.AddDays(7), // El token expira en una semana
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature // Algoritmo de cifrado estándar
                )
            };

            // Creación física del objeto Token
            var token = tokenHandler.CreateToken(tokenDescriptor);

            // Serialización a string (formato Base64)
            return tokenHandler.WriteToken(token);
        }
    }
}
