using EstateKit.Core.Models.ContactModels;

namespace EstateKit.Core.Models.UserModels.Business;

public partial class UserBusinessFinanceInstitution
{
    public long Id { get; set; }

    public long UserBusinessId { get; set; }

    public string InstitutionName { get; set; } = null!;

    public long? FinanceContactId { get; set; }

    public string? OnlineUsername { get; set; }

    public string? OnlinePassword { get; set; }

    public string? Notes { get; set; }

    public bool Active { get; set; }

    public virtual Contact? FinanceContact { get; set; }

    public virtual UserBusiness UserBusiness { get; set; } = null!;

    public virtual ICollection<UserBusinessFinanceAccount> UserBusinessFinanceAccounts { get;} = new List<UserBusinessFinanceAccount>();
}
