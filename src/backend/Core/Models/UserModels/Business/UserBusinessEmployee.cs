using System;
using System.Collections.Generic;
using EstateKit.Core.Models.ContactModels;

namespace EstateKit.Core.Models.UserModels.Business;

public partial class UserBusinessEmployee
{
    public long Id { get; set; }

    public long UserBusinessId { get; set; }

    public long EmployeeContactId { get; set; }

    public long EmployeePositionTypeId { get; set; }

    public string PositionName { get; set; } = null!;

    public bool Active { get; set; }

    public virtual Contact EmployeeContact { get; set; } = null!;

    public virtual EstateKit.Core.Models.Common.Type EmployeePositionType { get; set; } = null!;

    public virtual UserBusiness UserBusiness { get; set; } = null!;
}
