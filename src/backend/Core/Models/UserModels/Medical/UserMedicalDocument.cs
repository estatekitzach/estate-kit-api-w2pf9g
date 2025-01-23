using System;
using System.Collections.Generic;

namespace EstateKit.Core.Models.UserModels.Medical;

public partial class UserMedicalDocument
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long MedicalDocumentTypeId { get; set; }

    public string? DocLocation { get; set; }

    public bool Active { get; set; }

    public virtual EstateKit.Core.Models.Common.Type MedicalDocumentType { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
