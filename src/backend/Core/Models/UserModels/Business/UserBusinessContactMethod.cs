using EstateKit.Core.Models.Common; 

namespace EstateKit.Core.Models.UserModels.Business;

public partial class UserBusinessContactMethod
{
    public long Id { get; set; }

    public long UserBusinessId { get; set; }

    public long BusinessContactMethodId { get; set; }

    public bool IsDefault { get; set; }

    public bool Active { get; set; }

    public virtual BusinessContactMethod BusinessContactMethod { get; set; } = null!;

    public virtual UserBusiness UserBusiness { get; set; } = null!;
}
