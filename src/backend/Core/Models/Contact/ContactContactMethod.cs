using System;
using System.Collections.Generic;


namespace EstateKit.Core.Models.ContactModels;

/// <summary>
/// Lists the methods of contacting a contact. i.e. phone number, email
/// </summary>
public partial class ContactContactMethod
{
    public long Id { get; set; }

    public long ContactId { get; set; }

    public long ContactMethodTypeId { get; set; }

    public string ContactValue { get; set; } = null!;

    public bool IsDefault { get; set; }

    public bool Active { get; set; }

    public virtual Contact Contact { get; set; } = null!;

    public virtual Common.Type ContactMethodType { get; set; } = null!;
}
