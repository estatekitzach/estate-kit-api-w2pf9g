using EstateKit.Core.Models.Common; 

namespace EstateKit.Core.Models.UserModels.Business;

public partial class UserBusinessAddress
{
    public long Id { get; set; }

    public long UserBusinessId { get; set; }

    public long AddressId { get; set; }

    public bool IsDefault { get; set; }

    public bool Active { get; set; }

    public virtual Address Address { get; set; } = null!;

    public virtual UserBusiness UserBusiness { get; set; } = null!;
}
