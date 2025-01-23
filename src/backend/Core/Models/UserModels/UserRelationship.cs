

namespace EstateKit.Core.Models.UserModels;

public partial class UserRelationship
{
    public long Id { get; set; }

    public long PrimaryUserId { get; set; }

    public long RelatedUserId { get; set; }

    public long RelationshipTypeId { get; set; }

    public bool Active { get; set; }

    public bool IsTrusted { get; set; }

    public virtual User PrimaryUser { get; set; } = null!;

    public virtual User RelatedUser { get; set; } = null!;

    public virtual EstateKit.Core.Models.Common.Type RelationshipType { get; set; } = null!;
}
