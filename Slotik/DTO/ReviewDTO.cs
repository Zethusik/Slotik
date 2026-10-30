using Slotik.Models;
using System.ComponentModel.DataAnnotations;

namespace Slotik.DTO
{
    public class ReviewDTO
    {
        public int id { get; set; }
        public int rating { get; set; }
        public string? text { get; set; } = string.Empty;
        public DateTimeOffset? createdAt { get; set; }
        public string? clientFirstName { get; set; } = string.Empty; // TODO after getting rid of seed reviews remove nullable (?)
        public string? clientAvatarUrl { get; set; } = string.Empty; // TODO after getting rid of seed reviews remove nullable (?)
        public string serviceName { get; set; } = string.Empty ;
        public int? durationMin { get; set; } // TODO after getting rid of seed reviews remove nullable (?)
        public decimal? price {  get; set; } // TODO after getting rid of seed reviews remove nullable (?)
        public DateTimeOffset startsAt { get; set; }
        public DateTimeOffset endsAt { get;set; }
        public string? reply { get; set; } 
        public DateTimeOffset? UpdatedAt { get; set; }
        public List<ReviewPhotoDto> Photos { get; set; } = [];


    }

    public class ReviewPhotoDto
    {
        public int Id { get; set; }
        public string PhotoUrl { get; set; } = string.Empty;
    }

    public class BookingReviewDto
    {
        public int Id { get; set; }
        public int Rating { get; set; }
        public string Text { get; set; } = string.Empty;
        public List<ReviewPhotoDto> Photos { get; set; } = [];
    }

    public class CreateReviewDTO
    {
        [Required]
        public string Text { get; set; } = string.Empty;
        [Required]
        public int Rating { get; set; }

        public List<IFormFile>? Files { get; set; }
    }
}
