using EstateKit.Core.Models.ContactModels;

namespace EstateKit.Core.Models.UserModels;

public partial class UserContactRelationship
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long ContactId { get; set; }

    public long RelationshipTypeId { get; set; }

    public bool Active { get; set; }

    public virtual Contact Contact { get; set; } = null!;

    public virtual EstateKit.Core.Models.Common.Type RelationshipType { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
