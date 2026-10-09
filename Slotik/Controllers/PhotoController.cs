using Microsoft.AspNetCore.RateLimiting;
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
        private readonly IUserWriteLock _userLock;

        public PhotoController(IPhotoService photoService, AppDbContext context, IUserWriteLock userLock)
        {
            _context = context;
            _photoService = photoService;
            _userLock = userLock;
        }

        [HttpPost("upload")]
        [ServiceFilter(typeof(ImageUploadFilter))]
    [EnableRateLimiting("uploads")]
        [Authorize]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            var userid = User.FindFirstValue("userId");



            if (!int.TryParse(userid, out var userId))
                return Unauthorized();

            await using var transaction = await _userLock.AcquireAsync(userId, HttpContext.RequestAborted);
            var user = await _context.Users.FindAsync(userId);

            if (user == null) { return NotFound(new { message = "User Not found." }); }

            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<PhotoController>>();
            await using var batch = new PhotoUploadBatch(_photoService, logger);
            var result = await batch.AddAsync(file);

            if (PhotoUploadBatch.Failed(result))
                return BadRequest(new { error = "image_upload_failed" });

           

            var oldAsset = user.PhotoId;
            user.AvatarUrl = result.SecureUrl.AbsoluteUri;
            user.PhotoId = result.PublicId;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync(HttpContext.RequestAborted);
            batch.Commit();
            if (oldAsset != result.PublicId)
                await PhotoUploadBatch.DeleteBestEffortAsync(_photoService, oldAsset, logger);

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

            await using var transaction = await _userLock.AcquireAsync(userId, HttpContext.RequestAborted);
            var user =await _context.Users.FindAsync(userId);

            if (user == null) { return NotFound(new { message = "User Not found." }); }

            if (user.PhotoId == string.Empty) { return BadRequest(new { message = "User do not have avatar" }); }

            var oldAsset = user.PhotoId;
            user.PhotoId = string.Empty;
            user.AvatarUrl= string.Empty;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync(HttpContext.RequestAborted);
            await PhotoUploadBatch.DeleteBestEffortAsync(_photoService, oldAsset,
                HttpContext.RequestServices.GetRequiredService<ILogger<PhotoController>>());

            return Ok(new { message = "Photo deleted successfully" });
        }
    }
}
