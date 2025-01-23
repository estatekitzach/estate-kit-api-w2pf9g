using EstateKit.Core.Models.Common;

namespace EstateKit.Core.Models.UserModels;

public partial class UserDenomination
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long DenominationId { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public long? LocationAddressId { get; set; }

    public bool Active { get; set; }

    public virtual ReligiousDenomination Denomination { get; set; } = null!;

    public virtual Address? LocationAddress { get; set; }

    public virtual User User { get; set; } = null!;
}
