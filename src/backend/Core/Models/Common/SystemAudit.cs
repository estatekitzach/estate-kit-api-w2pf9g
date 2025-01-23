using System;
using System.Collections.Generic;

namespace EstateKit.Core.Models.Common;

public partial class SystemAudit
{
    public long Id { get; set; }

    public string ObjectName { get; set; } = null!;

    public long Identifier { get; set; }

    public string ColumnName { get; set; } = null!;

    public string OldValue { get; set; } = null!;

    public string NewValue { get; set; } = null!;
}
