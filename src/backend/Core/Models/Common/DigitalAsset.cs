using EstateKit.Core.Models.ContactModels;

namespace EstateKit.Core.Models.Common;

public partial class DigitalAsset
{
    public long Id { get; set; }

    public string? Provider { get; set; }

    public string? AccountNumber { get; set; }

    public string? SecondaryType { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? SystemType { get; set; }

    public long? ManagingContactId { get; set; }

    public bool IsIntellectualProperty { get; set; }

    public string? Instructions { get; set; }

    public string? Notes { get; set; }

    public long? FinanceInstructionTypeId { get; set; }

    public bool Active { get; set; }

    public virtual Type? FinanceInstructionType { get; set; }

    public virtual Contact? ManagingContact { get; set; }
}
