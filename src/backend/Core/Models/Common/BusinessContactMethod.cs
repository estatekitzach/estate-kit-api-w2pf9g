using EstateKit.Core.Models.UserModels.Business;

namespace EstateKit.Core.Models.Common;

public partial class BusinessContactMethod
{
    public long Id { get; set; }

    public string ContactValue { get; set; } = null!;

    public long ContactMethodTypeId { get; set; }

    public bool Active { get; set; }

    public virtual Type ContactMethodType { get; set; } = null!;

    public virtual ICollection<UserBusinessContactMethod> UserBusinessContactMethods { get;} = new List<UserBusinessContactMethod>();
}
