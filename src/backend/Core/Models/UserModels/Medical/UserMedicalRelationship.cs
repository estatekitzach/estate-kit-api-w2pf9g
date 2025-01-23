using EstateKit.Core.Models.ContactModels;

namespace EstateKit.Core.Models.UserModels.Medical;
public partial class UserMedicalRelationship
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long RelatedContactId { get; set; }

    public long RelationshipTypeId { get; set; }

    public string? RelationshipDescription { get; set; }

    public bool Active { get; set; }

    public virtual Contact RelatedContact { get; set; } = null!;

    public virtual EstateKit.Core.Models.Common.Type RelationshipType { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
