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
        private readonly EmailService _eservice; // Email service with email token generating, hashing and sending logic

        public AuthController(AppDbContext context,TokenService tservice, EmailService eservice)
        {
            _context = context;
            _tservice = tservice;
            _eservice = eservice;
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login([FromBody] LoginDTO dto) 
        {
            var email = dto.Email;
            var password = dto.Password;

            var user = await _context.Users.Include(u=>u.Master).FirstOrDefaultAsync(u => u.Email == email);

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

            user.LastName = dto.LastName;
            user.FirstName = dto.FirstName;
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

            var token = _eservice.GenerateEmailToken();

            var pending = new PendingRegistration { 
                FirstName = dto.FirstName, LastName = dto.LastName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role,
                PasswordHash = user.PasswordHash,
                TokenHash = _eservice.HashToken(token),
                ExpiresAt = DateTime.UtcNow.AddMinutes(30)
            };

            var ispending =_context.PendingRegistrations.FirstOrDefault(p => p.Email == pending.Email);

            if (ispending != null) 
            {
                _context.PendingRegistrations.Remove(ispending);
                await _context.SaveChangesAsync();
                return BadRequest("This Email is already on confirmation.");
            }

            await _context.PendingRegistrations.AddAsync(pending);
            await _context.SaveChangesAsync();

            var confirmationLink =
            $"https://localhost:7041/api/Auth/confirm?token={token}";

            await _eservice.SendConfirmationEmailAsync(user.Email, confirmationLink);

            return Ok("Check your Email for Confirmation link.");
        }

        [HttpGet("confirm")]
        public async Task<ActionResult> ConfirmEmail([FromQuery] string token) 
        {
            if (string.IsNullOrEmpty(token)) { return BadRequest("Invalid token"); }
            var tokenHash = _eservice.HashToken(token);

            var pending = await _context.PendingRegistrations.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
            if (pending == null) { return BadRequest("Link Expired or has Email has been already confirmed."); }

            if (pending.ExpiresAt < DateTime.UtcNow) {
            
                _context.PendingRegistrations.Remove(pending);
                await _context.SaveChangesAsync();

                return BadRequest("Confirmation link Expired.");
            }

            var user = await _context.Users.AnyAsync(u => u.Email == pending.Email);

            if (user)
            {
               
                return BadRequest("User already exists with the same Email.");
            }

            var Auser = new User
            {
                FirstName = pending.FirstName,
                LastName = pending.LastName,
                Email = pending.Email,
                Phone = pending.Phone,
                PasswordHash = pending.PasswordHash,
                Role = pending.Role,

            };

            _context.Users.Add(Auser);
            _context.PendingRegistrations.Remove(pending);
            await _context.SaveChangesAsync();
            return Ok("Email confirmed! You can now log in.");
        }

        [HttpPost("forgotPassword")]
        public async Task<ActionResult> sendCode([FromBody] PasswordRestoreDTOcs dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null) { return NotFound("No user with same mail address"); }

            var token = _eservice.GenerateEmailToken();
            PendingReset reset = new PendingReset
            {
                email = dto.Email,
                codeHash = _eservice.HashToken(token),
                codeExpiresAt = DateTime.UtcNow.AddMinutes(30),
                
            };

            await _context.PendingResets.AddAsync(reset);
            await _context.SaveChangesAsync();

            await _eservice.SendConfirmationCodeAsync(dto.Email, $"https://localhost:7041/api/Auth/confirmReset?token={token}");

            return Ok("Check your Email");

        }

        [HttpGet("confirmReset")]
        public async Task<ActionResult> confirmResetPassword([FromQuery] string token)
        {
            if (String.IsNullOrEmpty(token)) { return BadRequest("Bad Token."); }
            var hashedToken = _eservice.HashToken(token);

            var reset = await _context.PendingResets.FirstOrDefaultAsync(r => r.codeHash == hashedToken);

            if (reset == null) { return NotFound("Wrong token"); }

            if (reset.codeExpiresAt < DateTime.UtcNow)
            {

                _context.PendingResets.Remove(reset);
                await _context.SaveChangesAsync();

                return BadRequest("Confirmation link Expired.");
            }

            var FinalToken = _eservice.GenerateEmailToken();

            reset.finalTokenHash = _eservice.HashToken(FinalToken);
            reset.finalExpiresAt = DateTime.UtcNow.AddMinutes(30);
            await _context.SaveChangesAsync();

            return Ok(new { Token = FinalToken });



        }

        [HttpPost("resetPassword")]
        public async Task<ActionResult> resetPassword([FromBody] FinalResetDTO dto) 
        {
            var reset = await _context.PendingResets.FirstOrDefaultAsync(r=>r.finalTokenHash ==_eservice.HashToken(dto.Token));
            if (reset == null) { return NotFound("Invalid Token"); }

            if (reset.finalExpiresAt < DateTime.UtcNow)
            {
                _context.PendingResets.Remove(reset);
                await _context.SaveChangesAsync();

                return BadRequest("Token Expired.");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u=>u.Email==reset.email);

            if (user == null) { return BadRequest("User somehow deleted own account.");}

            user.PasswordHash = _tservice.HashSHA256(dto.NewPassword);
            await _context.SaveChangesAsync();

            return Ok("Password Changed.");

        }



    }
}
