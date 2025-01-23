using System;
using System.Collections.Generic;

namespace EstateKit.Core.Models.UserModels.Medical;

public partial class UserMedicalCondition
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string Condition { get; set; } = null!;

    public DateOnly? StartDate { get; set; }

    public bool Active { get; set; }

    public virtual User User { get; set; } = null!;
}
