
using EstateKit.Core.Models.Finance;

namespace EstateKit.Core.Models.UserModels;

public partial class UserFinanceAccount
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long FinanceAccountId { get; set; }

    public string InstitutionName { get; set; } = null!;

    public bool Active { get; set; }

    public virtual FinanceAccount FinanceAccount { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
