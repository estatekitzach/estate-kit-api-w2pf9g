using EstateKit.Core.Models.UserModels;

namespace EstateKit.Core.Models.Common;

/// <summary>
/// Lists the various relegiiou
/// </summary>
public partial class ReligiousDenomination
{
    public long Id { get; set; }

    public long ReligionTypeId { get; set; }

    public string Name { get; set; } = null!;

    public bool? Active { get; set; }

    public virtual Type ReligionType { get; set; } = null!;

    public virtual ICollection<UserDenomination> UserDenominations { get; } = new List<UserDenomination>();
}
