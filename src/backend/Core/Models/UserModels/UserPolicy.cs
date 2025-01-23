using EstateKit.Core.Models.ContactModels; 
namespace EstateKit.Core.Models.UserModels;

public partial class UserPolicy
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long PolicyTypeId { get; set; }

    public long? BeneficiaryContactId { get; set; }

    public string? AccountNumber { get; set; }

    public string? PlanName { get; set; }

    public string? GroupNumber { get; set; }

    public string? Notes { get; set; }

    public bool Active { get; set; }

    public virtual Contact? BeneficiaryContact { get; set; }

    public virtual EstateKit.Core.Models.Common.Type PolicyType { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
