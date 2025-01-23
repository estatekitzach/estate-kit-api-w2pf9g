
namespace EstateKit.Core.Models.UserModels;

public partial class UserFuneralGuestList
{
    public long Id { get; set; }

    public long UserFuneralId { get; set; }

    public string GuestName { get; set; } = null!;

    public string GuestPhone { get; set; } = null!;

    public string GuestEmail { get; set; } = null!;

    public string? GuestNotes { get; set; }

    public bool EulogyRequired { get; set; }

    public bool Active { get; set; }

    public virtual UserFuneral UserFuneral { get; set; } = null!;
}
