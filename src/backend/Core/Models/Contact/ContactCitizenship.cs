using EstateKit.Core.Models.Common;


namespace EstateKit.Core.Models.ContactModels;

/// <summary>
/// Details the contact&apos;s citizenship history
/// </summary>
public partial class ContactCitizenship
{
    public long Id { get; set; }

    public long ContactId { get; set; }

    public long CitizenTypeId { get; set; }

    public long CountryId { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public bool Active { get; set; }

    public virtual Common.Type CitizenType { get; set; } = null!;

    public virtual Contact Contact { get; set; } = null!;

    public virtual Country Country { get; set; } = null!;
}
