using EstateKit.Core.Models.Common;


namespace EstateKit.Core.Models.ContactModels;

/// <summary>
/// Link between contact and address
/// </summary>
public partial class ContactAddress
{
    public long Id { get; set; }

    public long ContactId { get; set; }

    public long AddressId { get; set; }

    public long AddressTypeId { get; set; }

    public bool IsDefault { get; set; }

    public bool? Active { get; set; }

    public virtual Address Address { get; set; } = null!;

    public virtual Common.Type AddressType { get; set; } = null!;

    public virtual Contact Contact { get; set; } = null!;
}
