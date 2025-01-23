

namespace EstateKit.Core.Models.UserModels;

public partial class UserDeathNotification
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string Name { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string Relationship { get; set; } = null!;

    public bool? Active { get; set; }

    public virtual User User { get; set; } = null!;
}
