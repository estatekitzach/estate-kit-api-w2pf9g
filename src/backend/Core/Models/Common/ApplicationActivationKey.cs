using EstateKit.Core.Models.UserModels;

namespace EstateKit.Core.Models.Common;

public partial class ApplicationActivationKey
{
    public long Id { get; set; }

    public string ActivationKey { get; set; } = null!;

    public string RecipientEmail { get; set; } = null!;

    public long? RecipientUserId { get; set; }

    public DateOnly? ActivationDate { get; set; }

    public string ActivationCustomerId { get; set; } = null!;

    public bool Active { get; set; }

    public virtual User? RecipientUser { get; set; }
}
