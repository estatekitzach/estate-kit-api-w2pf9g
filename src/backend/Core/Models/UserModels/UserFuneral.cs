using EstateKit.Core.Models.ContactModels;

namespace EstateKit.Core.Models.UserModels;

public partial class UserFuneral
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long FuneralInsuranceContactId { get; set; }

    public string? OtherPrePaidFuneralArrangment { get; set; }

    public long? BodyDisposalTypeId { get; set; }

    public long? CasketPreferenceTypeId { get; set; }

    public string? BurialLocation { get; set; }

    public bool IsBurialLocationPrePaid { get; set; }

    public string? MemorialStoneInstructions { get; set; }

    public string? CremationsLocation { get; set; }

    public bool FuneralRequired { get; set; }

    public string? FuneralHomeLocationName { get; set; }

    public string? FuneralHomeName { get; set; }

    public long? FuneralBudgetTypeId { get; set; }

    public decimal? FuneralMaxAmount { get; set; }

    public long? ClergyMemberContactId { get; set; }

    public string? BodyClothingInstructions { get; set; }

    public string? EulogyPreferences { get; set; }

    public string? CondolencePreference { get; set; }

    public bool LifeCelebrationRequired { get; set; }

    public long? CelebrationLocationAddressId { get; set; }

    public bool ShowVideo { get; set; }

    public string? VideoLocation { get; set; }

    public string? AdditionalInstructions { get; set; }

    public bool? Active { get; set; }

    public virtual EstateKit.Core.Models.Common.Type? BodyDisposalType { get; set; }

    public virtual EstateKit.Core.Models.Common.Type? CasketPreferenceType { get; set; }

    public virtual Contact? ClergyMemberContact { get; set; }

    public virtual EstateKit.Core.Models.Common.Type? FuneralBudgetType { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual ICollection<UserFuneralGuestList> UserFuneralGuestLists { get; } = new List<UserFuneralGuestList>();
}
