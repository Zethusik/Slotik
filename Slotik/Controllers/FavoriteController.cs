using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;

namespace Slotik.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FavoriteController : ControllerBase
{
    private readonly AppDbContext _context;

    public FavoriteController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var favorites = await _context.Set<Favorite>()
            .Include(f => f.User)
            .Include(f => f.Master)
            .ToListAsync();
        return Ok(favorites);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var favorite = await _context.Set<Favorite>()
            .Include(f => f.User)
            .Include(f => f.Master)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (favorite == null) return NotFound("Record not found");
        return Ok(favorite);
    }

    [HttpPost]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> Create([FromBody] CreateFavoriteDto dto)
    {
        var favorite = new Favorite
        {
            UserId = dto.UserId,
            MasterId = dto.MasterId
        };

        _context.Set<Favorite>().Add(favorite);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = favorite.Id }, favorite);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> Delete(int id)
    {
        var favorite = await _context.Set<Favorite>().FindAsync(id);
        if (favorite == null) return NotFound("Record not found");

        _context.Set<Favorite>().Remove(favorite);
        await _context.SaveChangesAsync();
        return Ok("Seen from selected");
    }
}