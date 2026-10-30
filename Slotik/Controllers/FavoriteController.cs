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
[Authorize(Roles = "Client,Superadmin")]
public class FavoriteController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IUserWriteLock _userWriteLock;

    public FavoriteController(AppDbContext context, IUserWriteLock userWriteLock)
    {
        _context = context;
        _userWriteLock = userWriteLock;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        if (User.UserId() is not int userId)
        {
            return Unauthorized();
        }

        var favorites = await _context.Favorites
            .AsNoTracking()
            .Where(favorite => favorite.UserId == userId)
            .Select(favorite => new FavoriteResponse(
                favorite.Id,
                favorite.UserId,
                favorite.MasterId))
            .ToListAsync(cancellationToken);

        return Ok(favorites);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        if (User.UserId() is not int userId)
        {
            return Unauthorized();
        }

        var favorite = await _context.Favorites
            .AsNoTracking()
            .Where(favorite => favorite.UserId == userId && favorite.Id == id)
            .Select(favorite => new FavoriteResponse(
                favorite.Id,
                favorite.UserId,
                favorite.MasterId))
            .SingleOrDefaultAsync(cancellationToken);

        if (favorite == null)
        {
            return NotFound("Record not found");
        }

        return Ok(favorite);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateFavoriteDto dto,
        CancellationToken cancellationToken)
    {
        if (User.UserId() is not int currentUserId)
        {
            return Unauthorized();
        }

        var ownerId = currentUserId;
        if (User.IsInRole("Superadmin"))
        {
            ownerId = dto.UserId;
        }

        if (!await _context.Users.AnyAsync(user => user.Id == ownerId, cancellationToken))
        {
            return NotFound(new { error = "user_not_found" });
        }

        await using var transaction = await _userWriteLock.AcquireAsync(
            ownerId,
            cancellationToken);

        var master = await _context.Masters
            .AsNoTracking()
            .Where(master => master.Id == dto.MasterId)
            .Select(master => new { master.IsBlocked })
            .SingleOrDefaultAsync(cancellationToken);

        if (master == null || master.IsBlocked)
        {
            return NotFound(new { error = "master_not_found" });
        }

        var existingFavorite = await _context.Favorites
            .AsNoTracking()
            .Where(favorite => favorite.UserId == ownerId && favorite.MasterId == dto.MasterId)
            .Select(favorite => new FavoriteResponse(
                favorite.Id,
                favorite.UserId,
                favorite.MasterId))
            .SingleOrDefaultAsync(cancellationToken);

        if (existingFavorite != null)
        {
            return Ok(existingFavorite);
        }

        var favorite = new Favorite
        {
            UserId = ownerId,
            MasterId = dto.MasterId
        };

        _context.Favorites.Add(favorite);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var response = new FavoriteResponse(favorite.Id, favorite.UserId, favorite.MasterId);
        var readAction = nameof(GetById);
        if (User.IsInRole("Superadmin"))
        {
            readAction = nameof(AdminGetById);
        }

        return CreatedAtAction(readAction, new { id = favorite.Id }, response);
    }

    [HttpGet("admin")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> AdminGetAll(CancellationToken cancellationToken)
    {
        var favorites = await _context.Favorites
            .AsNoTracking()
            .Select(favorite => new FavoriteResponse(
                favorite.Id,
                favorite.UserId,
                favorite.MasterId))
            .ToListAsync(cancellationToken);

        return Ok(favorites);
    }

    [HttpGet("admin/{id:int}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> AdminGetById(int id, CancellationToken cancellationToken)
    {
        var favorite = await _context.Favorites
            .AsNoTracking()
            .Where(favorite => favorite.Id == id)
            .Select(favorite => new FavoriteResponse(
                favorite.Id,
                favorite.UserId,
                favorite.MasterId))
            .SingleOrDefaultAsync(cancellationToken);

        if (favorite == null)
        {
            return NotFound();
        }

        return Ok(favorite);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (User.UserId() is not int userId)
        {
            return Unauthorized();
        }

        var favorites = _context.Favorites.Where(favorite => favorite.Id == id);
        if (!User.IsInRole("Superadmin"))
        {
            favorites = favorites.Where(favorite => favorite.UserId == userId);
        }

        var deletedCount = await favorites.ExecuteDeleteAsync(cancellationToken);
        if (deletedCount == 0)
        {
            return NotFound("Record not found");
        }

        return Ok("Seen from selected");
    }
}
