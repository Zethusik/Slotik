using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Slotik.Services
{
    public class PhotoService : IPhotoService
    {
        private readonly Lazy<Cloudinary> _cloudinary;

        public PhotoService(IOptions<CloudinarySettings> config)
        {
            _cloudinary = new Lazy<Cloudinary>(() => new Cloudinary(new Account(
                config.Value.CloudName,
                config.Value.ApiKey,
                config.Value.ApiSecret
            )));
        }

        public async Task<ImageUploadResult> AddPhotoAsync(IFormFile file)
        {
            var uploadResult = new ImageUploadResult();

            if (file != null && file.Length > 0)
            {
                using var stream = file.OpenReadStream();
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "slotik"
                };

                uploadResult = await _cloudinary.Value.UploadAsync(uploadParams);
            }

            return uploadResult;
        }

        public async Task<DeletionResult> DeletePhotoAsync(string publicId)
        {
            var deleteParams = new DeletionParams(publicId);
            return await _cloudinary.Value.DestroyAsync(deleteParams);
        }
    }
}
