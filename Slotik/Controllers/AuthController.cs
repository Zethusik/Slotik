using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;
using Slotik.Services;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Slotik.Models.Enums;

namespace Slotik.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {

        private readonly AppDbContext _context;
        private readonly TokenService _tservice;

        public AuthController(AppDbContext context,TokenService tservice)
        {
            _context = context;
            _tservice = tservice;
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login([FromBody] LoginDTO dto) 
        {
            var email = dto.Email;
            var password = dto.Password;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null) { return Unauthorized("Wrong Email or Password"); };

            

            if (user.PasswordHash == _tservice.HashSHA256(password))
            {
                var token = _tservice.GenerateToken(email, user.Role.ToString());

                return Ok(new { Token = token, Role = user.Role.ToString()});
            }
            else 
            {
                return Unauthorized("Wrong Email or Password");
            }
            
        }

        [HttpPost("register")]
        public async Task<ActionResult> Register([FromBody] RegisterDTO dto)
        {
            User user = new User();

            var exists = await _context.Users.AnyAsync(u => u.Phone == dto.Phone);
            if (exists) { return Conflict("User with the same Phone already exists"); }

            user.Phone = dto.Phone;

            exists = await _context.Users.AnyAsync(u => u.Name == dto.Name);
            if (exists) { return Conflict("User with the same Name already exists"); }

            user.Name = dto.Name;
            user.PasswordHash = _tservice.HashSHA256(dto.Password);

            exists = await _context.Users.AnyAsync(u => u.Email == dto.Email.Trim().ToLowerInvariant());
            if (exists) { return Conflict("User with the same Email already exists"); }

            user.Email = dto.Email.Trim().ToLowerInvariant();
            if (dto.Role == Models.Enums.UserRole.Superadmin.ToString()) { return Conflict("Cannot assign to SuperAdmin"); }

            if (dto.Role == "Client")
            {
                user.Role = UserRole.Client;
            }
            else 
            {
                user.Role = UserRole.Master;
            }

            

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
            var token = _tservice.GenerateToken(user.Email, user.Role.ToString());
            return Ok(new {Token= token,Role=user.Role.ToString()});
        }

        
    }
}
