using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;

namespace Slotik.Services
{
    public interface IPhotoService
    {
        Task<ImageUploadResult> AddPhotoAsync(IFormFile file);
        Task<DeletionResult> DeletePhotoAsync(string publicId);
    }
}