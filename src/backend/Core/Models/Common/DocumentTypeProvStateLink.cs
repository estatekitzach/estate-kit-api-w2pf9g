using System;
using System.Collections.Generic;

namespace EstateKit.Core.Models.Common;

public partial class DocumentTypeProvStateLink
{
    public long Id { get; set; }

    public long DocumentTypeId { get; set; }

    public long ProvStateId { get; set; }

    public string LinkText { get; set; } = null!;

#pragma warning disable CA1056 // URI-like properties should not be strings
    public string LinkUrl { get; set; } = null!;
#pragma warning restore CA1056 // URI-like properties should not be strings

    public bool Active { get; set; }

    public virtual Type DocumentType { get; set; } = null!;

    public virtual ProvState ProvState { get; set; } = null!;
}
