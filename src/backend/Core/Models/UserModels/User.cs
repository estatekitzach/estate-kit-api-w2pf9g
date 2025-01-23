
using EstateKit.Core.Models.Common;
using EstateKit.Core.Models.ContactModels;
using EstateKit.Core.Models.UserModels.Business;
using EstateKit.Core.Models.UserModels.Medical;

namespace EstateKit.Core.Models.UserModels;

public partial class User
{
    public long Id { get; set; }

    public long ContactId { get; set; }

    public string? KnownAs { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public long? BirthAddressId { get; set; }

    public long? KeyAccessInfoId { get; set; }

    public bool Active { get; set; }

    public long? MaritalStatusTypeId { get; set; }

    public virtual IReadOnlyCollection<ApplicationActivationKey> ApplicationActivationKeys { get; } = new List<ApplicationActivationKey>();

    public virtual Address? BirthAddress { get; set; }

    public virtual Contact Contact { get; set; } = null!;

    public virtual AccessInfo? KeyAccessInfo { get; set; }

    public virtual Common.Type? MaritalStatusType { get; set; }

    public virtual ICollection<UserAsset> UserAssets { get; } = new List<UserAsset>();

    public virtual ICollection<UserBusinessUserMap> UserBusinessUserMaps { get; } = new List<UserBusinessUserMap>();

    public virtual ICollection<UserCivilService> UserCivilServices { get; } = new List<UserCivilService>();

    public virtual ICollection<UserContactRelationship> UserContactRelationships { get; } = new List<UserContactRelationship>();

    public virtual ICollection<UserDeathNotification> UserDeathNotifications { get; } = new List<UserDeathNotification>();

    public virtual ICollection<UserDenomination> UserDenominations { get; } = new List<UserDenomination>();

    public virtual ICollection<UserDocument> UserDocuments { get; } = new List<UserDocument>();

    public virtual ICollection<UserFinanceAccount> UserFinanceAccounts { get; } = new List<UserFinanceAccount>();

    public virtual ICollection<UserFuneral> UserFunerals { get; } = new List<UserFuneral>();

    public virtual ICollection<UserIdentifier> UserIdentifiers { get; } = new List<UserIdentifier>();

    public virtual ICollection<UserInsurance> UserInsurances { get; } = new List<UserInsurance>();

    public virtual ICollection<UserMedicalAllergy> UserMedicalAllergies { get; } = new List<UserMedicalAllergy>();

    public virtual ICollection<UserMedicalAssistiveDevice> UserMedicalAssistiveDevices { get; } = new List<UserMedicalAssistiveDevice>();

    public virtual ICollection<UserMedicalCondition> UserMedicalConditions { get; } = new List<UserMedicalCondition>();

    public virtual ICollection<UserMedicalDocument> UserMedicalDocuments { get; } = new List<UserMedicalDocument>();

    public virtual ICollection<UserMedicalFamilyHistory> UserMedicalFamilyHistories { get; } = new List<UserMedicalFamilyHistory>();

    public virtual ICollection<UserMedicalMedication> UserMedicalMedications { get; } = new List<UserMedicalMedication>();

    public virtual ICollection<UserMedicalRelationship> UserMedicalRelationships { get; } = new List<UserMedicalRelationship>();

    public virtual ICollection<UserMedical> UserMedicals { get; } = new List<UserMedical>();

    public virtual ICollection<UserPolicy> UserPolicies { get; } = new List<UserPolicy>();

    public virtual ICollection<UserRelationship> UserRelationshipPrimaryUsers { get; } = new List<UserRelationship>();

    public virtual ICollection<UserRelationship> UserRelationshipRelatedUsers { get; } = new List<UserRelationship>();

    public virtual ICollection<UserSaasAppCredential> UserSaasAppCredentials { get; } = new List<UserSaasAppCredential>();
}
