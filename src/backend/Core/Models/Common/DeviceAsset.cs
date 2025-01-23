using System;
using System.Collections.Generic;

namespace EstateKit.Core.Models.Common;

public partial class DeviceAsset
{
    public long Id { get; set; }

    public string? DeviceName { get; set; }

    public string? NetworkPassword { get; set; }

    public string? SystemType { get; set; }

    public long? ManagingContactId { get; set; }

    public string? Instructions { get; set; }

    public string? Notes { get; set; }

    public bool Active { get; set; }
}
