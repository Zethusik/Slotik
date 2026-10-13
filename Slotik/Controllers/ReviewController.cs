using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.Mapping;
using Slotik.Data;
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
               .Where(r => r.Booking.MasterId == masterId.Value)
               .OrderByDescending(r => r.Booking.EndsAt)
               .ToListAsync(cancellationToken);

            if (reviews.Count == 0)
            {
                return NotFound(new
                {
                    message = "Reviews not found."
                });
            }

            reviews = reviews.OrderByDescending(r => r.Booking.EndsAt).ToList();

            return Ok(new
            {
                averageRating = reviews.Average(r => r.Rating),
                totalCount = reviews.Count(),
                Reviews = reviews
            });

        }

        [HttpPut("{Id}/reply")]
        [Authorize(Roles = "Master")]

        public async Task<ActionResult> Reply(int Id,string text) 
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

            var review = await _context.Reviews.Where(r => r.Booking.MasterId == masterId && r.Id == Id).FirstOrDefaultAsync();
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
    }
}
