using EstateKit.Core.Models.UserModels;
using EstateKit.Core.Models.UserModels.Business;

namespace EstateKit.Core.Models.Common;

public partial class AccessInfo
{
    public long Id { get; set; }

    public long KeyLocationId { get; set; }

    public string? AccessCode { get; set; }

    public string? AccessInstructions { get; set; }

    public bool Active { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public virtual Address KeyLocation { get; set; } = null!;

    public virtual ICollection<UserAsset> UserAssets { get; } = new List<UserAsset>();

    public virtual ICollection<UserBusinessAsset> UserBusinessAssets { get; } = new List<UserBusinessAsset>();

    public virtual ICollection<UserBusiness> UserBusinesses { get; } = new List<UserBusiness>();

    public virtual ICollection<User> Users { get; } = new List<User>();
}
