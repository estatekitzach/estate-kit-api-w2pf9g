using System;
using System.Collections.Generic;

namespace EstateKit.Core.Models.Common;

public partial class SystemRole
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string Code { get; set; } = null!;

    public bool Active { get; set; }
}
