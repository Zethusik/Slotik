using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;
using Slotik.Models.Enums;
using Slotik.Services;

namespace Slotik.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly TokenService _tservice;

        public UserController(AppDbContext context,TokenService tservice)
        {
            _context = context;
            _tservice = tservice;

        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult> GetAll()
        {
            var subs = await _context.Users.Include(u => u.Master).Include(u => u.Favorites).Include(u=>u.Notifications).Include(u=>u.Bookings).ToListAsync();
            return Ok(subs);
        }

        [HttpGet("{Id}")]
        [Authorize]
        public async Task<ActionResult> GetById(int id)
        {
            var sub = await _context.Users.Include(u => u.Master).Include(u => u.Favorites).Include(u => u.Notifications).Include(u => u.Bookings).FirstOrDefaultAsync(s => s.Id == id);
            if (sub == null) { return NotFound("User Not Found."); }

            return Ok(sub);
        }

        [HttpDelete("{Id}")]
        [Authorize]

        public async Task<ActionResult> DeleteById(int Id)
        {
            var sub = await _context.Users.FirstOrDefaultAsync(s => s.Id == Id);

            if (sub == null) { return NotFound("User Not Found."); }

            _context.Users.Remove(sub);
            _context.SaveChanges();

            return Ok("User is deleted");
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult> Create([FromBody] RegisterDTO dto)
        {
            UserRole role; 
            if (dto.Role == "Client")
            {
                role = UserRole.Client;
            }
            else
            {
                role = UserRole.Master;
            }
            User sub = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                Phone = dto.Phone,
                Role = role,
                PasswordHash = _tservice.HashSHA256(dto.Password),
                

            };

            _context.Users.Add(sub);
            await _context.SaveChangesAsync();
            return Ok(sub);
        }

        [HttpPut("{id}")]
        [Authorize]

        public async Task<ActionResult> Update(int id, [FromBody] RegisterDTO dto)
        {
            UserRole role;
            if (dto.Role == "Client")
            {
                role = UserRole.Client;
            }
            else
            {
                role = UserRole.Master;
            }

            var subToChange = await _context.Users.FirstOrDefaultAsync(s => s.Id == id);
            if (subToChange == null) { return NotFound("User Not Found."); }

            subToChange.Name = dto.Name;
            subToChange.Email = dto.Email;
            subToChange.PasswordHash = _tservice.HashSHA256(dto.Password);
            subToChange.Role = role;
            subToChange.Phone = dto.Phone;

            await _context.SaveChangesAsync();

            return Ok(subToChange);
        }



    }
}
