using System;
using System.Collections.Generic;

namespace EstateKit.Core.Models.Common;
public partial class DocumentIdentifierMap
{
    public long Id { get; set; }

    public string FieldName { get; set; } = null!;

    public long? IdentifierTypeId { get; set; }

    public long? UserDocumentTypeId { get; set; }

    public bool Active { get; set; }

    public virtual Type? IdentifierType { get; set; }

    public virtual Type? UserDocumentType { get; set; }
}
