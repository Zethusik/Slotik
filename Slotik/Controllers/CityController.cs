using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;

namespace Slotik.Controllers;

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
    public async Task<IActionResult> GetAll()
    {
        var cities = await _context.Cities
            .AsNoTracking()
            .Select(c => new
            {
                c.Id,
                c.Name,
                Districts = c.Districts.Select(d => new
                {
                    d.Id,
                    d.Name
                })
            })
            .ToListAsync();

        return Ok(cities);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var city = await _context.Cities
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.Name,
                Districts = c.Districts.Select(d => new
                {
                    d.Id,
                    d.Name
                })
            })
            .FirstOrDefaultAsync();

        if (city == null) return NotFound("City not found");

        return Ok(city);
    }
}