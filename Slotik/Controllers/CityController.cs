using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;

namespace Slotik.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CityController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CityController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult> GetCities()
        {
            return Ok(await _context.Cities.ToListAsync());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult> GetCityById(int id) 
        {
            var city = await _context.Cities.FindAsync(id);
            if (city == null) { return BadRequest("City not found"); }
            return Ok(city);
        }

        [HttpGet("{id}/districts")]
        public async Task<ActionResult> GetCityDistricts(int id)
        {
            var city = await _context.Cities.Include(c => c.Districts).FirstOrDefaultAsync(c=> c.Id==id);
            if (city == null | city.Districts == null) { return NotFound("Not found"); };

            var districts = city.Districts.Select(d => new
            {
                d.Id,
                d.Name
            });

            return Ok(districts);
        }
    }
}
