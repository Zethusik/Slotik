using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
        private readonly IConfiguration _configuration;
        private readonly PasswordService _passwords;
        private readonly IUserWriteLock _userLock;

        public AuthController(AppDbContext context,TokenService tservice, EmailService eservice, IConfiguration configuration, PasswordService passwords, IUserWriteLock userLock)
        {
            _context = context;
            _tservice = tservice;
            _eservice = eservice;
            _configuration = configuration;
            _passwords = passwords;
            _userLock = userLock;
        }

        [HttpPost("login")]
    [EnableRateLimiting("login")]
        public async Task<ActionResult> Login([FromBody] LoginDTO dto) 
        {
            var email = dto.Email;
            var password = dto.Password;

            var user = await _context.Users.Include(u=>u.Master).FirstOrDefaultAsync(u => u.Email == email);

            if (user == null) { return Unauthorized(new { message = "Wrong Email or Password" }); };

            

            var verification = _passwords.Verify(user.PasswordHash, password);
            if (verification != PasswordVerificationResult.Failed && user.Master?.IsBlocked != true)
            {
                if (verification == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    user.PasswordHash = _passwords.Hash(password);
                    await _context.SaveChangesAsync();
                }
                var token = _tservice.GenerateToken(user.Email, user.Role.ToString(),user.Id);

                return Ok(new { Token = token, Role = user.Role.ToString()});
            }
            else 
            {
                return Unauthorized(new { message = "Wrong Email or Password" });
            }
            
        }

        [HttpPost("register")]
    [EnableRateLimiting("recovery")]
        public async Task<ActionResult> Register([FromBody] RegisterDTO dto)
        {
            if (!_eservice.IsConfigured) return StatusCode(503, new { message = "Email delivery is not configured." });
            User user = new User();

            var exists = await _context.Users.AnyAsync(u => u.Phone == dto.Phone);
            if (exists) { return Conflict(new { message = "User with the same Phone already exists" }); }

            user.Phone = dto.Phone;

            user.LastName = dto.LastName;
            user.FirstName = dto.FirstName;
            user.PasswordHash = _passwords.Hash(dto.Password);

            exists = await _context.Users.AnyAsync(u => u.Email == dto.Email.Trim().ToLowerInvariant());
            if (exists) { return Conflict(new { message = "User with the same Email already exists" }); }

            user.Email = dto.Email.Trim().ToLowerInvariant();
            if (dto.Role == Models.Enums.UserRole.Superadmin.ToString()) { return Conflict(new { message = "Cannot assign to SuperAdmin" }); }

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
                return BadRequest(new { message = "This Email is already on confirmation." });
            }

            await _context.PendingRegistrations.AddAsync(pending);
            await _context.SaveChangesAsync();


            var confirmationLink =
            $"{_configuration["Frontend:BaseUrl"]}/confirm-email?token={Uri.EscapeDataString(token)}";

            await _eservice.SendConfirmationEmailAsync(user.Email, confirmationLink);

            return Ok(new { message = "Check your Email for Confirmation link." });
        }

        [HttpGet("confirm")]
        public async Task<ActionResult> ConfirmEmail([FromQuery] string token) 
        {
            if (string.IsNullOrEmpty(token)) { return BadRequest(new { message = "Invalid token" }); }
            var tokenHash = _eservice.HashToken(token);

            var pending = await _context.PendingRegistrations.FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
            if (pending == null) { return BadRequest(new { message = "Link Expired or has Email has been already confirmed." }); }

            if (pending.ExpiresAt < DateTime.UtcNow) {
            
                _context.PendingRegistrations.Remove(pending);
                await _context.SaveChangesAsync();

                return BadRequest(new { message = "Confirmation link Expired." });
            }

            var user = await _context.Users.AnyAsync(u => u.Email == pending.Email);

            if (user)
            {
               
                return BadRequest(new { message = "User already exists with the same Email." });
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
            return Ok(new { message = "Email confirmed! You can now log in." });
        }

        [HttpPost("forgotPassword")]
    [EnableRateLimiting("recovery")]
        public async Task<ActionResult> sendCode([FromBody] PasswordRestoreDTOcs dto)
        {
            if (!_eservice.IsConfigured) return StatusCode(503, new { message = "Email delivery is not configured." });
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null) { return NotFound(new { message = "No user with same mail address" }); }

            await using var transaction = await _userLock.AcquireAsync(user.Id, HttpContext.RequestAborted);
            if (!await _context.Users.AnyAsync(u => u.Id == user.Id && u.Email == user.Email)) return NotFound();

            var token = _eservice.GenerateEmailToken();
            PendingReset reset = new PendingReset
            {
                email = user.Email,
                codeHash = _eservice.HashToken(token),
                codeExpiresAt = DateTime.UtcNow.AddMinutes(30),
                
            };

            await _context.PendingResets.AddAsync(reset);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync(HttpContext.RequestAborted);

            await _eservice.SendConfirmationCodeAsync(dto.Email, $"{_configuration["Frontend:BaseUrl"]}/reset-password?token={Uri.EscapeDataString(token)}");

            return Ok(new { message = "Check your Email" });

        }

        [HttpGet("confirmReset")]
    [EnableRateLimiting("recovery")]
        public async Task<ActionResult> confirmResetPassword([FromQuery] string token)
        {
            if (String.IsNullOrEmpty(token)) { return BadRequest(new { message = "Bad Token." }); }
            var hashedToken = _eservice.HashToken(token);

            // Locate owner without tracking. Always re-read the request after acquiring the lock.
            var email = await _context.PendingResets.AsNoTracking().Where(r => r.codeHash == hashedToken)
                .Select(r => r.email).FirstOrDefaultAsync();
            var userId = await _context.Users.Where(u => u.Email == email).Select(u => (int?)u.Id).SingleOrDefaultAsync();
            if (userId == null) return NotFound(new { message = "Wrong token" });
            await using var transaction = await _userLock.AcquireAsync(userId.Value, HttpContext.RequestAborted);
            var reset = await _context.PendingResets.FirstOrDefaultAsync(r => r.codeHash == hashedToken && r.email == email);
            if (reset == null || reset.finalTokenHash != null) return NotFound(new { message = "Wrong token" });

            if (reset.codeExpiresAt <= DateTime.UtcNow)
            {

                _context.PendingResets.Remove(reset);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync(HttpContext.RequestAborted);

                return BadRequest(new { message = "Confirmation link Expired." });
            }

            var FinalToken = _eservice.GenerateEmailToken();

            reset.finalTokenHash = _eservice.HashToken(FinalToken);
            reset.finalExpiresAt = DateTime.UtcNow.AddMinutes(30);
            reset.codeHash = string.Empty;
            reset.codeExpiresAt = DateTime.MinValue;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync(HttpContext.RequestAborted);

            return Ok(new { Token = FinalToken });



        }

        [HttpPost("resetPassword")]
    [EnableRateLimiting("recovery")]
        public async Task<ActionResult> resetPassword([FromBody] FinalResetDTO dto) 
        {
            var hash = _eservice.HashToken(dto.Token);
            var email = await _context.PendingResets.AsNoTracking().Where(r => r.finalTokenHash == hash)
                .Select(r => r.email).FirstOrDefaultAsync();
            var userId = await _context.Users.Where(u => u.Email == email).Select(u => (int?)u.Id).SingleOrDefaultAsync();
            if (userId == null) return NotFound(new { message = "Invalid Token" });
            await using var transaction = await _userLock.AcquireAsync(userId.Value, HttpContext.RequestAborted);
            var reset = await _context.PendingResets.FirstOrDefaultAsync(r => r.finalTokenHash == hash && r.email == email);
            if (reset == null) { return NotFound(new { message = "Invalid Token" }); }

            if (reset.finalExpiresAt == null || reset.finalExpiresAt <= DateTime.UtcNow)
            {
                _context.PendingResets.Remove(reset);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync(HttpContext.RequestAborted);

                return BadRequest(new { message = "Token Expired." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value && u.Email == reset.email);

            if (user == null) { return BadRequest(new { message = "User somehow deleted own account." });}

            user.PasswordHash = _passwords.Hash(dto.NewPassword);

            // Includes both unconfirmed codes and issued final tokens for this owner.
            _context.PendingResets.RemoveRange(await _context.PendingResets.Where(r => r.email == user.Email).ToListAsync());

            await _context.SaveChangesAsync();
            await transaction.CommitAsync(HttpContext.RequestAborted);

            return Ok(new { message = "Password Changed." });

        }

        [HttpGet("Me")]
        [Authorize]

        public async Task<ActionResult> CheckBoard() 
        {
            if (User.UserId() is not int id) return Unauthorized();
            var user = await _context.Users
            .Include(u => u.Master)
                .ThenInclude(m => m!.Services)
            .Include(u => u.Master)
                .ThenInclude(m => m!.Schedules)
            .Include(u => u.Master)
                .ThenInclude(m => m!.Category)
            .Include(u => u.Master)
                .ThenInclude(m => m!.Subscriptions)
            .FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) { return NotFound(new { message = "User not found" });}



            var boarded = user.Master?.IsOnboardingCompleted == true;

            return Ok(new {
                userId = id,
                role = user.Role.ToString(),
                masterId = user.Master?.Id,
                isOnboardingCompleted = boarded
            });


        }



    }
}
