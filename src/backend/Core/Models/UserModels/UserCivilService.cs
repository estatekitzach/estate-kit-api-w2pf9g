using EstateKit.Core.Models.Common; 
namespace EstateKit.Core.Models.UserModels;

public partial class UserCivilService
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string ServiceName { get; set; } = null!;

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public bool? Active { get; set; }

    public string? BranchName { get; set; }

    public long? CountryId { get; set; }

    public virtual Country? Country { get; set; }

    public virtual User User { get; set; } = null!;
}
