using System.Linq.Expressions;
using Slotik.DTO;
using Slotik.Models;
using Slotik.Models.Enums;
namespace Slotik.Services;
public static class ApiResponses
{
    public static readonly Expression<Func<User, UserResponse>> User = u => new(
        u.Id, u.FirstName, u.LastName, u.Email, u.Phone, u.Role.ToString(),
        u.AvatarUrl, u.Master == null ? null : u.Master.Id);
    public static readonly Expression<Func<Service, PublicServiceResponse>> Service = s => new(
        s.Id, s.MasterId, s.Name, s.DurationMin, s.Price, s.Description, s.Included,
        s.GroupId, s.IsPopular, s.SortOrder,
        s.Photos.Select(p => new PublicPhotoResponse(p.Id, p.PhotoUrl, p.SortOrder)).ToList());
    public static readonly Expression<Func<Master, PublicMasterResponse>> Master = m => new()
    {
        Id = m.Id, Slug = m.Slug, FirstName = m.User.FirstName, LastName = m.User.LastName,
        AvatarUrl = m.User.AvatarUrl, About = m.About, ExperienceYears = m.ExperienceYears,
        SlotStepMin = m.SlotStepMin, IsBlocked = m.IsBlocked, CategoryId = m.CategoryId,
        CategoryName = m.Category.Name, DistrictId = m.DistrictId,
        DistrictName = m.District.Name, CityName = m.District.City.Name,
        Address = m.Address, Latitude = m.Latitude, Longitude = m.Longitude,
        Services = m.Services.Select(s => new PublicServiceResponse(s.Id, s.MasterId, s.Name,
            s.DurationMin, s.Price, s.Description, s.Included, s.GroupId, s.IsPopular, s.SortOrder,
            s.Photos.Select(p => new PublicPhotoResponse(p.Id, p.PhotoUrl, p.SortOrder)).ToList())).ToList(),
        PortfolioPhotos = m.PortfolioPhotos.Select(p => new PublicPhotoResponse(p.Id, p.PhotoUrl, 0)).ToList()
    };
    public static PaymentResponse Payment(Payment p) => new(p.Id, p.OrderId, p.CreatedAt,
        p.PaidAt, p.Amount, p.Currency, p.Status, p.ProviderStatus, p.SubscriptionId, p.EntitlementReviewRequired);
    public static SubscriptionResponse Subscription(Subscription s, LiqPayService prices, int? effectiveId) => new(s.Id, s.MasterId,
        s.Plan, s.Status, s.ExpiresAt, s.IsTrial, s.Payments.Select(Payment).ToList(),
        s.Plan.ToString(), s.Plan == SubscriptionPlan.Free ? 0 : prices.GetPrice(s.Plan), "UAH",
        s.Plan == SubscriptionPlan.Free ? null : "month", null, null, s.Id == effectiveId);
}
