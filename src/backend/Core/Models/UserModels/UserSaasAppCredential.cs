
namespace EstateKit.Core.Models.UserModels;

public partial class UserSaasAppCredential
{
    public long Id { get; set; }

    public long UserId { get; set; }

#pragma warning disable CA1056 // URI-like properties should not be strings
    public string SaasUrl { get; set; } = null!;
#pragma warning restore CA1056 // URI-like properties should not be strings

    public string SaasUsername { get; set; } = null!;

    public string SaasPwd { get; set; } = null!;

    public bool Active { get; set; }

    public virtual User User { get; set; } = null!;
}
