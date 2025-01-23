using EstateKit.Core.Models.Common;
namespace EstateKit.Core.Models.UserModels.Business;

public partial class UserBusinessAsset
{
    public long Id { get; set; }

    public long UserBusinessId { get; set; }

    public long AssetTypeId { get; set; }

    public long AssetId { get; set; }

    public long? AccessInfoId { get; set; }

    public string AssetName { get; set; } = null!;

    public bool Active { get; set; }

    public string? Notes { get; set; }

    public virtual AccessInfo? AccessInfo { get; set; }

    public virtual Common.Type AssetType { get; set; } = null!;

    public virtual UserBusiness UserBusiness { get; set; } = null!;
}
