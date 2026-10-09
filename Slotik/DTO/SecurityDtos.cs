using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Slotik.Models.Enums;
namespace Slotik.DTO;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class UpdateUserProfileDto
{
    [Required, MaxLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string LastName { get; set; } = string.Empty;
    [Required, Phone, MaxLength(32)] public string Phone { get; set; } = string.Empty;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CreateNotificationDto
{
    [Required, MaxLength(100)] public string Type { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string Text { get; set; } = string.Empty;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class UpdateNotificationDto { public bool IsRead { get; set; } }
public record UserResponse(int Id, string FirstName, string LastName, string Email,
    string Phone, string Role, string AvatarUrl, int? MasterId);
public record NotificationResponse(int Id, string Type, string Text, bool IsRead, DateTime CreatedAt);
public record FavoriteResponse(int Id, int UserId, int MasterId);
public record PublicPhotoResponse(int Id, string PhotoUrl, int SortOrder);
public record PublicServiceResponse(int Id, int MasterId, string Name, int DurationMin,
    decimal Price, string? Description, string? Included, int? GroupId, bool IsPopular,
    int SortOrder, List<PublicPhotoResponse> Photos);
public class PublicMasterResponse
{
    public int Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string AvatarUrl { get; init; } = string.Empty;
    public string? About { get; init; }
    public int ExperienceYears { get; init; }
    public int SlotStepMin { get; init; }
    public bool IsBlocked { get; init; }
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public int DistrictId { get; init; }
    public string DistrictName { get; init; } = string.Empty;
    public string CityName { get; init; } = string.Empty;
    public string? Address { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public List<PublicServiceResponse> Services { get; init; } = new();
    public List<PublicPhotoResponse> PortfolioPhotos { get; init; } = new();
}
public record PaymentResponse(int Id, string OrderId, DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt, decimal Amount, string Currency, PaymentStatus Status,
    string? ProviderStatus, int SubscriptionId, bool EntitlementReviewRequired);
public record SubscriptionResponse(int Id, int MasterId, SubscriptionPlan Plan,
    SubscriptionStatus Status, DateTimeOffset ExpiresAt, bool IsTrial, List<PaymentResponse> Payments,
    string PlanName, decimal Price, string Currency, string? BillingPeriod,
    DateTimeOffset? NextPaymentAt, decimal? NextPaymentAmount, bool IsEffective);
