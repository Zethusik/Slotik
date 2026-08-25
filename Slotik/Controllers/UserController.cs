using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Slotik.Data;
using Slotik.Models;

namespace Slotik.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        public UserController(AppDbContext context) {
            _context = context;
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<User>> GetUserById(int id) 
        {

            return await _context.Users.FindAsync(id);

        }

        [HttpPost]
        public async Task<ActionResult<User>> AddUser(User user) 
        {
            try
            {
                await _context.Users.AddAsync(user);
                await _context.SaveChangesAsync();
                return Ok(user);
            }
            catch (Exception ex) { 
                return BadRequest(ex.Message);
            } 
                
        }

        

    }
}
