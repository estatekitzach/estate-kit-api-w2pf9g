using EstateKit.Core.Models.ContactModels;

namespace EstateKit.Core.Models.UserModels.Medical;

public partial class UserMedical
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long? HealthCareRep1ContactId { get; set; }

    public long? HealthCareRep2ContactId { get; set; }

    public long? PrimaryPhysicianContactId { get; set; }

    public string? PreferredHospital { get; set; }

    public string? LocationOfHealthCareCard { get; set; }

    public bool Active { get; set; }

    public long? PhoneContactId { get; set; }

    public string? HealthCareNumber { get; set; }

    public long? HealthCareProvStateId { get; set; }

    public string? BloodType { get; set; }

    public string? Height { get; set; }

    public string? Weight { get; set; }

    public bool VaccinationUpToDate { get; set; }

    public string VaccinationCardLocation { get; set; } = null!;

    public virtual Contact? HealthCareRep1Contact { get; set; }

    public virtual Contact? HealthCareRep2Contact { get; set; }

    public virtual Contact? PhoneContact { get; set; }

    public virtual Contact? PrimaryPhysicianContact { get; set; }

    public virtual User User { get; set; } = null!;
}
