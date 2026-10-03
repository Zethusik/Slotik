using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Slotik.Data;
using Slotik.Services;
using System.Security.Claims;

namespace Slotik.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    
    public class PhotoController : ControllerBase
    {
        private readonly IPhotoService _photoService;
        private readonly AppDbContext _context;

        public PhotoController(IPhotoService photoService, AppDbContext context)
        {
            _context = context;
            _photoService = photoService;
        }

        [HttpPost("upload")]
        [Authorize]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            var userid = User.FindFirstValue("userId");



            if (!int.TryParse(userid, out var userId))
                return Unauthorized();

            var user = await _context.Users.FindAsync(userId);

            if (user == null) { return NotFound(new { message = "User Not found." }); }

            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            var result = await _photoService.AddPhotoAsync(file);

            if (result.Error != null)
                return BadRequest(result.Error.Message);

           

            user.AvatarUrl = result.SecureUrl.AbsoluteUri;
            user.PhotoId = result.PublicId;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                url = result.SecureUrl.AbsoluteUri,
                publicId = result.PublicId
            });
        }

        [HttpDelete("delete")]
        [Authorize]
        public async Task<IActionResult> Delete()
        {
            var userid = User.FindFirstValue("userId");
            if (!int.TryParse(userid, out var userId))
                return Unauthorized();

            var user =await _context.Users.FindAsync(userId);

            if (user == null) { return NotFound(new { message = "User Not found." }); }

            if (user.PhotoId == string.Empty) { return BadRequest(new { message = "User do not have avatar" }); }

            var result = await _photoService.DeletePhotoAsync(user.PhotoId);

            if (result.Error != null)
                return BadRequest(result.Error.Message);

            user.PhotoId = string.Empty;
            user.AvatarUrl= string.Empty;
            _context.SaveChanges();

            return Ok(new { message = "Photo deleted successfully" });
        }
    }
}