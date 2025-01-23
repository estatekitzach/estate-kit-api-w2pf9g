using System;
using System.Collections.Generic;

namespace EstateKit.Core.Models.UserModels.Medical;

public partial class UserMedicalFamilyHistory
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string FamilyMemberName { get; set; } = null!;

    public string Relation { get; set; } = null!;

    public string Condition { get; set; } = null!;

    public short? AgeAtOnset { get; set; }

    public bool? IsMaternal { get; set; }

    public bool Active { get; set; }

    public virtual User User { get; set; } = null!;
}
