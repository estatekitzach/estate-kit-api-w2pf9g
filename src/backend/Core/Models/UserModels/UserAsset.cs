using EstateKit.Core.Models.Common; 

namespace EstateKit.Core.Models.UserModels;


public partial class UserAsset
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long? AccessInfoId { get; set; }

    public string? Name { get; set; }

    public long AssetTypeId { get; set; }

    public long? LocationAddressId { get; set; }

    public bool Active { get; set; }

    public long? AssetId { get; set; }

    public string? AssetName { get; set; }

    public virtual AccessInfo? AccessInfo { get; set; }

    public virtual Common.Type AssetType { get; set; } = null!;

    public virtual Address? LocationAddress { get; set; }

    public virtual User User { get; set; } = null!;
}
