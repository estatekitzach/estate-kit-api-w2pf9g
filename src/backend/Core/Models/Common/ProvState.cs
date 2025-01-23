using System;
using System.Collections.Generic;

namespace EstateKit.Core.Models.Common;

/// <summary>
/// Lists the states and provinces of countries
/// </summary>
public partial class ProvState
{
    public long Id { get; set; }

    public long CountryId { get; set; }

    public string Name { get; set; } = null!;

    public string Code { get; set; } = null!;

    public bool Active { get; set; }

    public virtual ICollection<Address> Addresses { get;} = new List<Address>();

    public virtual Country Country { get; set; } = null!;

    public virtual ICollection<DocumentTypeProvStateLink> DocumentTypeProvStateLinks { get;} = new List<DocumentTypeProvStateLink>();
}
