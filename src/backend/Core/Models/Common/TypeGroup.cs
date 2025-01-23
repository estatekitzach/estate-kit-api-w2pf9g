using System;
using System.Collections.Generic;

namespace EstateKit.Core.Models.Common;

/// <summary>
/// Used to hold the type groups
/// </summary>
public partial class TypeGroup
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string Key { get; set; } = null!;

    public bool Active { get; set; }

    public virtual ICollection<Type> Types { get;} = new List<Type>();
}
