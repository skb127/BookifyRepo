using Bookify.Domain.Abstractions;

namespace Bookify.Domain.CancellationPolicies;

public sealed class CancellationPolicy : Entity
{
    private CancellationPolicy(
        Guid id,
        string name,
        decimal earlyGuestPenaltyRate,
        decimal lateGuestPenaltyRate,
        decimal earlyHostPenaltyRate,
        decimal lateHostPenaltyRate,
        int thresholdHours,
        bool isDefault,
        DateTime createdOnUtc)
        : base(id)
    {
        Name = name;
        EarlyGuestPenaltyRate = earlyGuestPenaltyRate;
        LateGuestPenaltyRate = lateGuestPenaltyRate;
        EarlyHostPenaltyRate = earlyHostPenaltyRate;
        LateHostPenaltyRate = lateHostPenaltyRate;
        ThresholdHours = thresholdHours;
        IsDefault = isDefault;
        CreatedOnUtc = createdOnUtc;
    }

    private CancellationPolicy()
    {
    }

    public string Name { get; private set; } = null!;
    public decimal EarlyGuestPenaltyRate { get; private set; }
    public decimal LateGuestPenaltyRate { get; private set; }
    public decimal EarlyHostPenaltyRate { get; private set; }
    public decimal LateHostPenaltyRate { get; private set; }
    public int ThresholdHours { get; private set; }
    public bool IsDefault { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }

    public static CancellationPolicy Create(
        string name,
        decimal earlyGuestPenaltyRate,
        decimal lateGuestPenaltyRate,
        decimal earlyHostPenaltyRate,
        decimal lateHostPenaltyRate,
        int thresholdHours,
        bool isDefault,
        DateTime utcNow) =>
        new(
            Guid.CreateVersion7(),
            name,
            earlyGuestPenaltyRate,
            lateGuestPenaltyRate,
            earlyHostPenaltyRate,
            lateHostPenaltyRate,
            thresholdHours,
            isDefault,
            utcNow);
}
