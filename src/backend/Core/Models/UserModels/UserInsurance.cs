using EstateKit.Core.Models.ContactModels;

namespace EstateKit.Core.Models.UserModels;

public partial class UserInsurance
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long InsuranceTypeId { get; set; }

    public string InsuranceCompanyName { get; set; } = null!;

    public string? PlanName { get; set; }

    public string? GroupNumber { get; set; }

    public string? MemberServicesPhone { get; set; }

    public string? LocationOfCard { get; set; }

    public long? PhoneContactId { get; set; }

    public bool Active { get; set; }

    public virtual EstateKit.Core.Models.Common.Type InsuranceType { get; set; } = null!;

    public virtual Contact? PhoneContact { get; set; }

    public virtual User User { get; set; } = null!;
}
