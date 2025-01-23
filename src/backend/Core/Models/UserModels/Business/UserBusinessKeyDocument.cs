using System;
using System.Collections.Generic;

namespace EstateKit.Core.Models.UserModels.Business;

public partial class UserBusinessKeyDocument
{
    public long Id { get; set; }

    public long UserBusinessId { get; set; }

    public long? BuDocumentTypeId { get; set; }

    public string? OtherName { get; set; }

    public string? Location { get; set; }

    public bool Active { get; set; }

    public virtual EstateKit.Core.Models.Common.Type? BuDocumentType { get; set; }

    public virtual UserBusiness UserBusiness { get; set; } = null!;
}
