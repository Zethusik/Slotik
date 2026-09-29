using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;
using Slotik.Models.Enums;

namespace Slotik.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceGroupController : ControllerBase
    {

        private readonly AppDbContext _context;

        public ServiceGroupController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetGroups(int categoryId)
        {
            var categoryExists = await _context.Categories
                .AnyAsync(c => c.Id == categoryId);

            if (!categoryExists)
            {
                return NotFound("Category Not Found");
            }

            var groups = await _context.ServiceGroups
                .Where(g => g.CategoryId == categoryId)
                .Select(g => new
                {
                    id = g.Id,
                    name = g.Name,
                    categoryId = g.CategoryId
                })
                .ToListAsync();

            return Ok(groups);

        }
        [HttpPost]
        [Authorize(Roles = "Superadmin")]

        public async Task<IActionResult> createGroup([FromBody] CreateServiceGroupDTO dto) 
        {
            if (await _context.Categories.FirstOrDefaultAsync(c => c.Id == dto.CategoryId) == null) { return NotFound("Invalid CategoryId"); }

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest("Group name is required");
            }

            var exists = await _context.ServiceGroups
                    .AnyAsync(g =>
                        g.CategoryId == dto.CategoryId &&
                        g.Name.ToLower() == dto.Name.Trim().ToLower());

            if (exists)
            {
                return BadRequest("Group already exists in this category");
            }

            ServiceGroup group = new ServiceGroup {
                Name=dto.Name,
                CategoryId=dto.CategoryId,
            
            };

            await _context.ServiceGroups.AddAsync(group);
            await _context.SaveChangesAsync();

            return Ok(group);
        }
    }
}
