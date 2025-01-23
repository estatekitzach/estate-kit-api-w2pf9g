
namespace EstateKit.Core.Models.UserModels;

public partial class UserDocument
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long DocumentTypeId { get; set; }

    public string? DigitalFrontPhotoLocation { get; set; }

    public string? DigitalBackPhotoLocation { get; set; }

    public bool Relevant { get; set; }

    public string? Location { get; set; }

    public bool InKit { get; set; }

    public bool Active { get; set; }

    public virtual EstateKit.Core.Models.Common.Type DocumentType { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
