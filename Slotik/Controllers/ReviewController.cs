using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.Mapping;
using Slotik.Data;
using Slotik.DTO;
using Slotik.Models;
using Slotik.Models.Enums;
using System.Security.Claims;
using System.Threading;

namespace Slotik.Controllers
{
    [Route("api/[controller]")]
    [ApiController]


    public class ReviewController : ControllerBase
    {

        private readonly AppDbContext _context;

        public ReviewController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("master/me")]
        [Authorize(Roles = "Master")]

        public async Task<ActionResult> getReviews(CancellationToken cancellationToken)
        {
            var userid = User.FindFirstValue("userId");

            if (!int.TryParse(userid, out var UserId))
                return Unauthorized();

            var masterId = await _context.Masters
                .Where(m => m.UserId == UserId)
                .Select(m => (int?)m.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (masterId == null) { return NotFound(new { message = "Master profile is missing" }); }

            var reviews = await _context.Reviews
               .AsNoTracking()
               .Include(r=>r.User)
               .Include(r=>r.Booking)
                .ThenInclude(r=>r.Service)
               .Where(r => r.Booking.MasterId == masterId.Value)
               .ToListAsync(cancellationToken);

            List<ReviewDTO> FormRevs = new List<ReviewDTO>();

            if (reviews.Count == 0)
            {
                return Ok(new
                {
                    averageRating = 0,
                    totalCount = 0,
                    Reviews = Array.Empty<ReviewDTO>(),
                });
            }



            foreach (var review in reviews)
            {
                FormRevs.Add(new ReviewDTO {
                    id = review.Id,
                    rating = review.Rating,
                    text = review.Text,
                    createdAt = review.CreatedAt,
                    clientFirstName = review.User?.FirstName,
                    clientAvatarUrl = review.User?.AvatarUrl,
                    serviceName = review.Booking.Service.Name,
                    durationMin = review.DurationMin,
                    price = review.Price,
                    startsAt = review.Booking.StartsAt,
                    endsAt = review.Booking.EndsAt,
                    reply = review.Reply,
                    UpdatedAt = review.UpdatedAt,
                
                });
            }

            FormRevs = FormRevs.OrderByDescending(r => r.createdAt.HasValue).ThenByDescending(r=>r.createdAt).ToList();

            return Ok(new
            {
                averageRating = FormRevs.Average(r => r.rating),
                totalCount = FormRevs.Count,
                Reviews = FormRevs
            });

        }

        [HttpPut("{Id:int}/reply")]
        [Authorize(Roles = "Master")]

        public async Task<ActionResult> Reply(int Id,[FromBody]string text) 
        {
            var now = DateTimeOffset.UtcNow;
            if (string.IsNullOrWhiteSpace(text)) { return BadRequest(new { message="Text is essential for reply."}); }

            var userid = User.FindFirstValue("userId");

            if (!int.TryParse(userid, out var UserId))
                return Unauthorized();

            var masterId = await _context.Masters
                .Where(m => m.UserId == UserId)
                .Select(m => (int?)m.Id)
                .FirstOrDefaultAsync();

            if (masterId == null) { return NotFound(new { message = "Master profile is missing" }); }

            var review = await _context.Reviews.Include(r=>r.Booking).Where(r => r.Booking.MasterId == masterId && r.Id == Id).FirstOrDefaultAsync();
            if (review == null) { return NotFound(new { message = "Review Not Found."}); }

            review.Reply = text.Trim();
            review.UpdatedAt = now;

            await _context.SaveChangesAsync();

            return Ok(new {
            reviewId = Id,
            reply = text.Trim(),
            reviewUpdatedAt = now
            });

            

        }

        [HttpGet("master/{Id:int}")]

        public async Task<ActionResult> MasterReviews(int Id, CancellationToken cancellationToken) 
        {

            var master = await _context.Masters
                .Where(m => m.Id == Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (master == null) { return NotFound(new { message="Master Not Found."}); }

            var reviews = await _context.Reviews
              .AsNoTracking()
              .Include(r => r.User)
              .Include(r => r.Booking)
               .ThenInclude(r => r.Service)
              .Where(r => r.Booking.MasterId == master.Id)
              .ToListAsync(cancellationToken);

            List<ReviewDTO> FormRevs = new List<ReviewDTO>();

            if (reviews.Count == 0)
            {
                return Ok(new
                {
                   averageRating = 0,
                   totalCount = 0,
                   Reviews = Array.Empty<ReviewDTO>(),
                });
            }



            foreach (var review in reviews)
            {
                FormRevs.Add(new ReviewDTO
                {
                    id = review.Id,
                    rating = review.Rating,
                    text = review.Text,
                    createdAt = review.CreatedAt,
                    clientFirstName = review.User?.FirstName,
                    clientAvatarUrl = review.User?.AvatarUrl,
                    serviceName = review.Booking.Service.Name,
                    durationMin = review.DurationMin,
                    price = review.Price,
                    startsAt = review.Booking.StartsAt,
                    endsAt = review.Booking.EndsAt,
                    reply = review.Reply,
                    UpdatedAt = review.UpdatedAt

                });
            }

            FormRevs = FormRevs.OrderByDescending(r => r.createdAt.HasValue).ThenByDescending(r => r.createdAt).ToList();

            return Ok(new
            {
                averageRating = FormRevs.Average(r => r.rating),
                totalCount = FormRevs.Count,
                Reviews = FormRevs
            });



        }

        [HttpPut("booking/{Id:int}")]
        [Authorize(Roles = "Client")]

        public async Task<ActionResult> CreateReview(int Id, [FromBody]CreateReviewDTO dto) 
        {
            if (dto.Rating < 1 || dto.Rating > 5)
            {
                return BadRequest(new
                {
                    message = "Rating must be between 1 and 5."
                });
            }
            var userid = User.FindFirstValue("userId");

            if (!int.TryParse(userid, out var UserId))
                return Unauthorized();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == UserId);

            if (user == null) { return NotFound(new { message="User Profile Not Found."}); }
            var booking = await _context.Bookings.Include(b=>b.Service).Include(b=>b.Review).Where(b => b.Id == Id && b.UserId == UserId).FirstOrDefaultAsync();

            if (booking == null) { return NotFound(new { message = "Booking not found."}); }

            if (string.IsNullOrWhiteSpace(dto.Text)) { return BadRequest(new { message="Text is required for review."}); }

            if (booking.Status != BookingStatus.Completed)
            {
                return Conflict(new
                {
                    message = "Review can be added only after completed booking."
                });
            }

            if (booking.Review == null)
            {
                Review review = new Review
                {
                    Rating = dto.Rating,
                    Text = dto.Text.Trim(),
                    UserId = UserId,
                    Price = booking.Service.Price,
                    DurationMin = booking.Service.DurationMin,
                    BookingId = booking.Id,
                    CreatedAt = DateTimeOffset.UtcNow,

                };

                await _context.Reviews.AddAsync(review);
                await _context.SaveChangesAsync();

            }
            else
            { 
                booking.Review.Rating = dto.Rating;
                booking.Review.Text = dto.Text.Trim();
                booking.Review.UpdatedAt = DateTimeOffset.UtcNow;
                await _context.SaveChangesAsync();
            }

            
            return Ok(new {Text = dto.Text.Trim(),Rating=dto.Rating,Price = booking.Service.Price,DurationMin=booking.Service.DurationMin });

        }
    }
}
