using System;
using System.Collections.Generic;

namespace EstateKit.Core.Models.UserModels.Medical;

public partial class UserMedicalAllergy
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string AllergicTo { get; set; } = null!;

    public string Reaction { get; set; } = null!;

    public bool Active { get; set; }

    public virtual User User { get; set; } = null!;
}
