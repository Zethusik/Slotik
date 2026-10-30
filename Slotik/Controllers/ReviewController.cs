using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;
using Slotik.Models.Enums;
using Slotik.Services;

namespace Slotik.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewController : ControllerBase
    {
        private const int MaximumPhotosPerReview = 3;

        private readonly AppDbContext _context;
        private readonly IPhotoService _photoService;
        private readonly IUserWriteLock _userWriteLock;

        public ReviewController(
            AppDbContext context,
            IPhotoService photoService,
            IUserWriteLock userWriteLock)
        {
            _context = context;
            _photoService = photoService;
            _userWriteLock = userWriteLock;
        }

        [HttpGet("master/me")]
        [Authorize(Roles = "Master")]
        public async Task<ActionResult> GetReviews(CancellationToken cancellationToken)
        {
            if (User.UserId() is not int userId)
            {
                return Unauthorized();
            }

            var masterId = await _context.Masters
                .Where(master => master.UserId == userId)
                .Select(master => (int?)master.Id)
                .SingleOrDefaultAsync(cancellationToken);

            if (masterId == null)
            {
                return NotFound(new { message = "Master profile is missing" });
            }

            return await GetMasterReviewsAsync(masterId.Value, cancellationToken);
        }

        [HttpPut("{Id:int}/reply")]
        [Authorize(Roles = "Master")]
        public async Task<ActionResult> Reply(int Id, [FromBody] string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return BadRequest(new { message = "Text is essential for reply." });
            }

            if (User.UserId() is not int userId)
            {
                return Unauthorized();
            }

            var masterId = await _context.Masters
                .Where(master => master.UserId == userId)
                .Select(master => (int?)master.Id)
                .SingleOrDefaultAsync();

            if (masterId == null)
            {
                return NotFound(new { message = "Master profile is missing" });
            }

            var review = await _context.Reviews
                .Where(review => review.Booking.MasterId == masterId && review.Id == Id)
                .SingleOrDefaultAsync();

            if (review == null)
            {
                return NotFound(new { message = "Review Not Found." });
            }

            var now = DateTimeOffset.UtcNow;
            review.Reply = text.Trim();
            review.UpdatedAt = now;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                reviewId = Id,
                reply = review.Reply,
                reviewUpdatedAt = now
            });
        }

        [HttpGet("master/{Id:int}")]
        public async Task<ActionResult> MasterReviews(int Id, CancellationToken cancellationToken)
        {
            var masterExists = await _context.Masters
                .AnyAsync(master => master.Id == Id, cancellationToken);

            if (!masterExists)
            {
                return NotFound(new { message = "Master Not Found." });
            }

            return await GetMasterReviewsAsync(Id, cancellationToken);
        }

        [HttpPut("booking/{Id:int}")]
        [ServiceFilter(typeof(ImageUploadFilter))]
        [Consumes("multipart/form-data")]
        [Authorize(Roles = "Client")]
        public async Task<ActionResult> CreateReview(
            int Id,
            [FromForm] CreateReviewDTO dto,
            CancellationToken cancellationToken)
        {
            if (dto.Rating < 1 || dto.Rating > 5)
            {
                return BadRequest(new { message = "Rating must be between 1 and 5." });
            }

            if (string.IsNullOrWhiteSpace(dto.Text))
            {
                return BadRequest(new { message = "Text is required for review." });
            }

            if (User.UserId() is not int userId)
            {
                return Unauthorized();
            }

            await using var transaction = await _userWriteLock.AcquireAsync(
                userId,
                cancellationToken);

            var booking = await _context.Bookings
                .Include(booking => booking.Service)
                .Include(booking => booking.Review)
                    .ThenInclude(review => review!.Files)
                .Where(booking => booking.Id == Id && booking.UserId == userId)
                .SingleOrDefaultAsync(cancellationToken);

            if (booking == null)
            {
                return NotFound(new { message = "Booking not found." });
            }

            if (booking.Review?.UserId is int reviewOwnerId && reviewOwnerId != userId)
            {
                return NotFound(new { message = "Review Not Found." });
            }

            if (booking.Status != BookingStatus.Completed)
            {
                return Conflict(new
                {
                    message = "Review can be added only after completed booking."
                });
            }

            var existingPhotoCount = booking.Review?.Files.Count ?? 0;
            var newPhotoCount = dto.Files?.Count ?? 0;
            if (existingPhotoCount + newPhotoCount > MaximumPhotosPerReview)
            {
                return BadRequest(new
                {
                    error = "review_photo_limit_exceeded",
                    message = "A review can contain at most 3 photos, including existing photos."
                });
            }

            await using var batch = new PhotoUploadBatch(
                _photoService,
                HttpContext.RequestServices.GetRequiredService<ILogger<ReviewController>>());

            var review = booking.Review;
            if (review == null)
            {
                review = new Review
                {
                    UserId = userId,
                    Price = booking.Service.Price,
                    DurationMin = booking.Service.DurationMin,
                    BookingId = booking.Id,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                _context.Reviews.Add(review);
            }
            else
            {
                review.UpdatedAt = DateTimeOffset.UtcNow;
            }

            review.Rating = dto.Rating;
            review.Text = dto.Text.Trim();

            if (dto.Files != null)
            {
                foreach (var file in dto.Files)
                {
                    var result = await batch.AddAsync(file);
                    if (PhotoUploadBatch.Failed(result))
                    {
                        return BadRequest(new { error = "image_upload_failed" });
                    }

                    review.Files.Add(new ReviewPhoto
                    {
                        PhotoId = result.PublicId,
                        PhotoUrl = result.SecureUrl.AbsoluteUri
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            batch.Commit();

            var photos = review.Files
                .OrderBy(photo => photo.Id)
                .Select(photo => new ReviewPhotoDto
                {
                    Id = photo.Id,
                    PhotoUrl = photo.PhotoUrl
                })
                .ToList();

            return Ok(new
            {
                Text = review.Text,
                Rating = review.Rating,
                Price = booking.Service.Price,
                DurationMin = booking.Service.DurationMin,
                Photos = photos
            });
        }

        private async Task<ActionResult> GetMasterReviewsAsync(
            int masterId,
            CancellationToken cancellationToken)
        {
            var reviews = await _context.Reviews
                .AsNoTracking()
                .Where(review => review.Booking.MasterId == masterId)
                .OrderByDescending(review => review.CreatedAt.HasValue)
                .ThenByDescending(review => review.CreatedAt)
                .Select(review => new ReviewDTO
                {
                    id = review.Id,
                    rating = review.Rating,
                    text = review.Text,
                    createdAt = review.CreatedAt,
                    clientFirstName = review.User == null ? null : review.User.FirstName,
                    clientAvatarUrl = review.User == null ? null : review.User.AvatarUrl,
                    serviceName = review.Booking.Service.Name,
                    durationMin = review.DurationMin,
                    price = review.Price,
                    startsAt = review.Booking.StartsAt,
                    endsAt = review.Booking.EndsAt,
                    reply = review.Reply,
                    UpdatedAt = review.UpdatedAt,
                    Photos = review.Files
                        .OrderBy(photo => photo.Id)
                        .Select(photo => new ReviewPhotoDto
                        {
                            Id = photo.Id,
                            PhotoUrl = photo.PhotoUrl
                        })
                        .ToList()
                })
                .ToListAsync(cancellationToken);

            var averageRating = 0.0;
            if (reviews.Count > 0)
            {
                averageRating = reviews.Average(review => review.rating);
            }

            return Ok(new
            {
                averageRating,
                totalCount = reviews.Count,
                Reviews = reviews
            });
        }
    }
}
