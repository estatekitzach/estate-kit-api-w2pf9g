using EstateKit.Core.Models.UserModels.Business;
using EstateKit.Core.Models.UserModels;

namespace EstateKit.Core.Models.Finance;

public partial class FinanceAccount
{
    public long Id { get; set; }

    public string BranchNumber { get; set; } = null!;

    public string AccountNumber { get; set; } = null!;

    public long AccountTypeId { get; set; }

    public string? Notes { get; set; }

    public bool Active { get; set; }

    public virtual Common.Type AccountType { get; set; } = null!;

    public virtual ICollection<FinanceAccountHistory> FinanceAccountHistories { get; } = new List<FinanceAccountHistory>();

    public virtual ICollection<UserBusinessFinanceAccount> UserBusinessFinanceAccounts { get; } = new List<UserBusinessFinanceAccount>();

    public virtual ICollection<UserFinanceAccount> UserFinanceAccounts { get; } = new List<UserFinanceAccount>();
}
