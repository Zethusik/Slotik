using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;
using Slotik.Services;

namespace Slotik.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
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
        if (User.UserId() is not int userId) return Unauthorized();
        var favorites = await _context.Set<Favorite>()
            .Where(f => f.UserId == userId)
            .Select(f => new FavoriteResponse(f.Id, f.UserId, f.MasterId))
            .ToListAsync();
        return Ok(favorites);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (User.UserId() is not int userId) return Unauthorized();
        var favorite = await _context.Set<Favorite>()
            .Where(f => f.UserId == userId && f.Id == id)
            .Select(f => new FavoriteResponse(f.Id, f.UserId, f.MasterId))
            .FirstOrDefaultAsync();

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

        return CreatedAtAction(nameof(AdminGetById), new { id = favorite.Id }, new FavoriteResponse(favorite.Id, favorite.UserId, favorite.MasterId));
    }

    [HttpGet("admin")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> AdminGetAll() => Ok(await _context.Favorites
        .Select(f => new FavoriteResponse(f.Id, f.UserId, f.MasterId)).ToListAsync());

    [HttpGet("admin/{id:int}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> AdminGetById(int id)
    {
        var favorite = await _context.Favorites.Where(f => f.Id == id)
            .Select(f => new FavoriteResponse(f.Id, f.UserId, f.MasterId)).SingleOrDefaultAsync();
        return favorite == null ? NotFound() : Ok(favorite);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> Delete(int id)
    {
        if (User.UserId() is not int userId) return Unauthorized();
        var favorite = await _context.Set<Favorite>().FindAsync(id);
        if (favorite == null) return NotFound("Record not found");

        _context.Set<Favorite>().Remove(favorite);
        await _context.SaveChangesAsync();
        return Ok("Seen from selected");
    }
}
