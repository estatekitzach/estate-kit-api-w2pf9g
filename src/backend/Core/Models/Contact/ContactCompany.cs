using EstateKit.Core.Models.Common; 


namespace EstateKit.Core.Models.ContactModels;

public partial class ContactCompany
{
    public long Id { get; set; }

    public long ContactId { get; set; }

    public long CompanyId { get; set; }

    public string? Occupation { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public bool Active { get; set; }

    public virtual Company Company { get; set; } = null!;
}
