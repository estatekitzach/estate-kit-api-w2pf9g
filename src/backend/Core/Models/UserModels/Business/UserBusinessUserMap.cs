using System;
using System.Collections.Generic;

namespace EstateKit.Core.Models.UserModels.Business;

public partial class UserBusinessUserMap
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long UserBusinessId { get; set; }

    public bool Active { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual UserBusiness UserBusiness { get; set; } = null!;
}
