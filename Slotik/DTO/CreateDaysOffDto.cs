namespace Slotik.DTO;

using System.ComponentModel.DataAnnotations;
using Slotik.Services;

public class CreateDaysOffDto : IValidatableObject
{
    public DateOnly DateFrom { get; set; }

    public DateOnly DateTo { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Full local days need representable UTC offsets and the exclusive next-day boundary.
        if (DateFrom <= DateOnly.MinValue || DateTo >= DateOnly.MaxValue || DateFrom > DateTo)
        {
            yield return new ValidationResult(
                "Dates must be 0001-01-02..9999-12-30 with DateFrom <= DateTo.", [nameof(DateFrom), nameof(DateTo)]);
            yield break;
        }
        var zone = BookingRules.KyivTimeZone();
        if (zone.IsInvalidTime(DateFrom.ToDateTime(TimeOnly.MinValue))
            || zone.IsInvalidTime(DateTo.AddDays(1).ToDateTime(TimeOnly.MinValue)))
            yield return new ValidationResult("Local day boundary does not exist in Europe/Kyiv.", [nameof(DateFrom), nameof(DateTo)]);
    }
}
