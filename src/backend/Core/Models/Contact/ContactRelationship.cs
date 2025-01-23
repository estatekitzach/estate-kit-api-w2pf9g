using System;
using System.Collections.Generic;

namespace EstateKit.Core.Models.ContactModels;
public partial class ContactRelationship
{
    public long Id { get; set; }

    public long ContactId { get; set; }

    public long RelatedContactId { get; set; }

    public long RelationshipTypeId { get; set; }

    public bool Active { get; set; }

    public virtual Contact Contact { get; set; } = null!;

    public virtual Contact RelatedContact { get; set; } = null!;

    public virtual Common.Type RelationshipType { get; set; } = null!;
}
