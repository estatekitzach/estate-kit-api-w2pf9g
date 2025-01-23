using System;
using System.Collections.Generic;
using EstateKit.Core.Models.Finance;

namespace EstateKit.Core.Models.UserModels.Business;
public partial class UserBusinessFinanceAccount
{
    public long Id { get; set; }

    public long UserBusinessFinanceInstId { get; set; }

    public long FinanceAccountId { get; set; }

    public string? Notes { get; set; }

    public bool Active { get; set; }

    public virtual FinanceAccount FinanceAccount { get; set; } = null!;

    public virtual UserBusinessFinanceInstitution UserBusinessFinanceInst { get; set; } = null!;
}
