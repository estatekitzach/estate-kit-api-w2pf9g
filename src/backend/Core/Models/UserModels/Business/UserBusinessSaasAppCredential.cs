using System;
using System.Collections.Generic;

namespace EstateKit.Core.Models.UserModels.Business;

public partial class UserBusinessSaasAppCredential
{
    public long Id { get; set; }

    public long UserBusinessId { get; set; }

#pragma warning disable CA1056 // URI-like properties should not be strings
    public string SassUrl { get; set; } = null!;
#pragma warning restore CA1056 // URI-like properties should not be strings

    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public bool Active { get; set; }

    public virtual UserBusiness UserBusiness { get; set; } = null!;
}
