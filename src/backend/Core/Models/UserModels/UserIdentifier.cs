
namespace EstateKit.Core.Models.UserModels;

public partial class UserIdentifier
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long IdentifierTypeId { get; set; }

    public string Identifier { get; set; } = null!;

    public DateOnly? ExpiryDate { get; set; }

    public bool Active { get; set; }

    public virtual Common.Type IdentifierType { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
