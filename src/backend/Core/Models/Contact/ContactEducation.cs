using System;
using System.Collections.Generic;


namespace EstateKit.Core.Models.ContactModels;

public partial class ContactEducation
{
    public long Id { get; set; }

    public long ContactId { get; set; }

    public string EducationFacilityName { get; set; } = null!;

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public bool HasCompleted { get; set; }

    public bool Active { get; set; }

    public virtual Contact Contact { get; set; } = null!;
}
