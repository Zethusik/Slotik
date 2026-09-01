using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;

namespace Slotik.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DaysOffController : ControllerBase
{
    private readonly AppDbContext _context;

    public DaysOffController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var daysOff = await _context.Set<DaysOff>().ToListAsync();
        return Ok(daysOff);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var dayOff = await _context.Set<DaysOff>().FindAsync(id);
        if (dayOff == null) return NotFound("Entry about weekends not found");
        return Ok(dayOff);
    }

    [HttpPost]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> Create([FromBody] CreateDaysOffDto dto)
    {
        var dayOff = new DaysOff
        {
            DateFrom = dto.DateFrom,
            DateTo = dto.DateTo,
            MasterId = dto.MasterId
        };

        _context.Set<DaysOff>().Add(dayOff);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = dayOff.Id }, dayOff);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateDaysOffDto dto)
    {
        var dayOff = await _context.Set<DaysOff>().FindAsync(id);
        if (dayOff == null) return NotFound("Entry about weekends not found");

        dayOff.DateFrom = dto.DateFrom;
        dayOff.DateTo = dto.DateTo;
        dayOff.MasterId = dto.MasterId;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> Delete(int id)
    {
        var dayOff = await _context.Set<DaysOff>().FindAsync(id);
        if (dayOff == null) return NotFound("Entry about weekends not found");

        _context.Set<DaysOff>().Remove(dayOff);
        await _context.SaveChangesAsync();
        return Ok("Entry deleted");
    }
}