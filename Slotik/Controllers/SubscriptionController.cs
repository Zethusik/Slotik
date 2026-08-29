using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;

namespace Slotik.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SubscriptionController : ControllerBase
    {

        private readonly AppDbContext _context;

        public SubscriptionController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult> GetAll() {
            var subs = await _context.Subscriptions.Include(s => s.Payments).Include(s => s.Master).ToListAsync();
            return Ok(subs);
        }

        [HttpGet("{Id}")]
        [Authorize]
        public async Task<ActionResult> GetById(int id)
        {
            var sub = await _context.Subscriptions.Include(s => s.Payments).Include(s => s.Master).FirstOrDefaultAsync(s => s.Id == id);
            if (sub == null) { return NotFound("Subscription Not Found."); }

            return Ok(sub);
        }

        [HttpDelete("{Id}")]
        [Authorize]

        public async Task<ActionResult> DeleteById(int Id)
        {
            var sub = await _context.Subscriptions.FirstOrDefaultAsync(s => s.Id == Id);

            if (sub == null) { return NotFound("Subscription Not Found."); }

            _context.Subscriptions.Remove(sub);
            _context.SaveChanges();

            return Ok("Subscription is deleted");
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult> Create([FromBody] CreateSubscriptionDTO dto) 
        {
            Subscription sub = new Subscription { 
                MasterId = dto.MasterId,
                Plan = dto.Plan,
                Status = dto.Status,
                ExpiresAt = dto.ExpiresAt,
                
            };

            _context.Subscriptions.Add(sub);
            await _context.SaveChangesAsync();
            return Ok(sub);
        }

        [HttpPut("{id}")]
        [Authorize]

        public async Task<ActionResult> Update(int id, [FromBody] CreateSubscriptionDTO dto) 
        {
            var subToChange = await _context.Subscriptions.FirstOrDefaultAsync(s => s.Id == id);
            if (subToChange == null) { return NotFound("Subscription Not Found."); }

            subToChange.MasterId = dto.MasterId;
            subToChange.Plan = dto.Plan;
            subToChange.Status = dto.Status;
            subToChange.ExpiresAt = dto.ExpiresAt;

            await _context.SaveChangesAsync();

            return Ok(subToChange);
        }
    }
}
