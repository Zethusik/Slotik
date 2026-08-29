using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;

namespace Slotik.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MasterController : ControllerBase
{
    private readonly AppDbContext _context;

    public MasterController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll()
    {
        var masters = await _context.Masters
            .Include(m => m.User)
            .Include(m => m.Category)
            .Include(m => m.District)
            .ToListAsync();
        return Ok(masters);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetById(int id)
    {
        var master = await _context.Masters
            .Include(m => m.User)
            .Include(m => m.Category)
            .Include(m => m.District)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (master == null) return NotFound("Technician not found");
        return Ok(master);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateMasterDto dto)
    {
        var master = new Master
        {
            UserId = dto.UserId,
            CategoryId = dto.CategoryId,
            DistrictId = dto.DistrictId,
            Slug = dto.Slug,
            About = dto.About,
            ExperienceYears = dto.ExperienceYears,
            SlotStepMin = dto.SlotStepMin,
            IsBlocked = dto.IsBlocked
        };

        _context.Masters.Add(master);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = master.Id }, master);
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] CreateMasterDto dto)
    {
        var master = await _context.Masters.FindAsync(id);
        if (master == null) return NotFound("Technician not found");

        master.Slug = dto.Slug;
        master.About = dto.About;
        master.ExperienceYears = dto.ExperienceYears;
        master.SlotStepMin = dto.SlotStepMin;
        master.IsBlocked = dto.IsBlocked;
        master.CategoryId = dto.CategoryId;
        master.DistrictId = dto.DistrictId;
        master.UserId = dto.UserId;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var master = await _context.Masters.FindAsync(id);
            if (master == null) return NotFound("Technician not found");

        _context.Masters.Remove(master);
        await _context.SaveChangesAsync();
        return Ok("Technician deleted");
    }
}
