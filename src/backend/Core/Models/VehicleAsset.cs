using System;
using System.Collections.Generic;

namespace EstateKit.Core.Models;

public partial class VehicleAsset
{
    public long Id { get; set; }

    public string Make { get; set; } = null!;

    public string Model { get; set; } = null!;

    public short Year { get; set; }

    public string? Colour { get; set; }

    public string? Vin { get; set; }

    public bool Active { get; set; }
}
