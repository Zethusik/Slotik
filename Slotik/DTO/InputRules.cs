using System.ComponentModel.DataAnnotations;

namespace Slotik.DTO;

public sealed class SlotStepAttribute : ValidationAttribute
{
    public SlotStepAttribute() => ErrorMessage = "SlotStepMin must be 5..240 and divisible by 5.";
    public override bool IsValid(object? value) => value is int step && step is >= 5 and <= 240 && step % 5 == 0;
}
