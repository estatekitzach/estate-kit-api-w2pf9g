using EstateKit.Core.Models.ContactModels;

namespace EstateKit.Core.Models.UserModels.Business;
public partial class UserBusinessVendor
{
    public long Id { get; set; }

    public long UserBusinessId { get; set; }

    public string VendorName { get; set; } = null!;

    public long VendorServiceTypeId { get; set; }

    public long VendorContactId { get; set; }

    public string? Notes { get; set; }

    public bool Active { get; set; }

    public virtual Contact VendorContact { get; set; } = null!;

    public virtual EstateKit.Core.Models.Common.Type VendorServiceType { get; set; } = null!;
}
