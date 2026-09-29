using Microsoft.AspNetCore.Mvc;
using Slotik.Services;

namespace Slotik.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PhotoController : ControllerBase
    {
        private readonly IPhotoService _photoService;

        public PhotoController(IPhotoService photoService)
        {
            _photoService = photoService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            var result = await _photoService.AddPhotoAsync(file);

            if (result.Error != null)
                return BadRequest(result.Error.Message);

            return Ok(new
            {
                url = result.SecureUrl.AbsoluteUri,
                publicId = result.PublicId
            });
        }

        [HttpDelete("{publicId}")]
        public async Task<IActionResult> Delete(string publicId)
        {
            var result = await _photoService.DeletePhotoAsync(publicId);

            if (result.Error != null)
                return BadRequest(result.Error.Message);

            return Ok(new { message = "Photo deleted successfully" });
        }
    }
}