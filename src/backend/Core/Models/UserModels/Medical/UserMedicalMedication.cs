using System;
using System.Collections.Generic;

namespace EstateKit.Core.Models.UserModels.Medical;

public partial class UserMedicalMedication
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string Name { get; set; } = null!;

    public string Dosage { get; set; } = null!;

    public string Frequency { get; set; } = null!;

    public string? SideEffect { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public bool Active { get; set; }

    public virtual User User { get; set; } = null!;
}
