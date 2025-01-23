using System;
using System.Collections.Generic;
using EstateKit.Core.Models.Finance;

namespace EstateKit.Core.Models.UserModels.Business;

public partial class UserBusinessCreditCard
{
    public long Id { get; set; }

    public long UserBusinessId { get; set; }

    public long FinanceCreditCardId { get; set; }

    public string? Notes { get; set; }

    public bool Active { get; set; }

    public virtual FinanceCreditCard FinanceCreditCard { get; set; } = null!;

    public virtual UserBusiness UserBusiness { get; set; } = null!;
}
