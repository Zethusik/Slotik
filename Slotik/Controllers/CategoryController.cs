using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.Models;

namespace Slotik.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CategoryController : ControllerBase
{
    private readonly AppDbContext _context;

    public CategoryController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/Category
    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _context.Categories
            .Select(c => new
            {
                id = c.Id,
                name = c.Name,
                icon = c.Icon,
                mastersCount = _context.Masters.Count(m => m.CategoryId == c.Id)
            })
            .ToListAsync();

        return Ok(categories);
    }

    // POST /api/Category
    [HttpPost]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Category name is required" });

        var category = new Category
        {
            Name = dto.Name,
            Icon = dto.Icon ?? "default-icon"
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return Ok(category);
    }

    // PUT /api/Category/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryDto dto)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return NotFound(new { message = "Category not found" });

        category.Name = dto.Name;
        if (!string.IsNullOrEmpty(dto.Icon))
            category.Icon = dto.Icon;

        await _context.SaveChangesAsync();
        return Ok(category);
    }

    // DELETE /api/Category/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Superadmin")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return NotFound(new { message = "Category not found" });

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Category deleted successfully" });
    }
}

public class CategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
}