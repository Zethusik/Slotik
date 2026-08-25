using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;

namespace Slotik.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DistrictsController : ControllerBase
{
    private readonly AppDbContext _context;

    public DistrictsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var districts = await _context.Districts
            .Include(d => d.City)
            .ToListAsync();
        return Ok(districts);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var district = await _context.Districts
            .Include(d => d.City)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (district == null) return NotFound("Area not found");
        return Ok(district);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDistrictDto dto)
    {
        var district = new District
        {
            Name = dto.Name,
            CityId = dto.CityId
        };

        _context.Districts.Add(district);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = district.Id }, district);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateDistrictDto dto)
    {
        var district = await _context.Districts.FindAsync(id);
        if (district == null) return NotFound("Area not found");

        district.Name = dto.Name;
        district.CityId = dto.CityId;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var district = await _context.Districts.FindAsync(id);
        if (district == null) return NotFound("Area not found");

        _context.Districts.Remove(district);
        await _context.SaveChangesAsync();
        return Ok("District deleted");
    }
}