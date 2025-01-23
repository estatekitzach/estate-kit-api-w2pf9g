using System;
using System.Collections.Generic;

namespace EstateKit.Core.Models.Finance;

public partial class FinanceAccountHistory
{
    public long Id { get; set; }

    public long FinanceAccountId { get; set; }

    public double Balance { get; set; }

    public DateOnly BalanceDate { get; set; }

    public bool Active { get; set; }

    public virtual FinanceAccount FinanceAccount { get; set; } = null!;
}
