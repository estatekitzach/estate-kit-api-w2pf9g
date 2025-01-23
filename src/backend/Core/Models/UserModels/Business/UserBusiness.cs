using EstateKit.Core.Models.Common; 


namespace EstateKit.Core.Models.UserModels.Business;

public partial class UserBusiness
{
    public long Id { get; set; }

    public string BusinessName { get; set; } = null!;

    public string LegalBusinessName { get; set; } = null!;

    public string Description { get; set; } = null!;

    public long BusinessInterestTypeId { get; set; }

    public bool IsOwned { get; set; }

    public long AccessInfoId { get; set; }

    public long OwnershipTypeId { get; set; }

    public double? BusinessValue { get; set; }

    public DateOnly? BusinessValueDate { get; set; }

    public bool RoyaltyEntitlement { get; set; }

    public bool BuySellAgreement { get; set; }

    public string? SigningOfficers { get; set; }

    public string? BusinessValuatorBuName { get; set; }

    public string? BusinessValuatorContactName { get; set; }

    public string? BusinessValuatorPhone { get; set; }

    public long? DispositionWishTypeId { get; set; }

    public string? PossiblePurchaserName { get; set; }

    public string? PossiblePurchaserEmail { get; set; }

    public string? PossiblePurchaserPhone { get; set; }

    public long? PossiblePurchaserAddressId { get; set; }

    public string? NotesInstructions { get; set; }

    public bool Active { get; set; }

    public virtual AccessInfo AccessInfo { get; set; } = null!;

    public virtual Common.Type BusinessInterestType { get; set; } = null!;

    public virtual Common.Type? DispositionWishType { get; set; }

    public virtual Common.Type OwnershipType { get; set; } = null!;

    public virtual Address? PossiblePurchaserAddress { get; set; }

    public virtual ICollection<UserBusinessAddress> UserBusinessAddresses { get; } = new List<UserBusinessAddress>();

    public virtual ICollection<UserBusinessAsset> UserBusinessAssets { get; } = new List<UserBusinessAsset>();

    public virtual ICollection<UserBusinessContactMethod> UserBusinessContactMethods { get; } = new List<UserBusinessContactMethod>();

    public virtual ICollection<UserBusinessCreditCard> UserBusinessCreditCards { get; } = new List<UserBusinessCreditCard>();

    public virtual ICollection<UserBusinessEmployee> UserBusinessEmployees { get; } = new List<UserBusinessEmployee>();

    public virtual ICollection<UserBusinessFinanceInstitution> UserBusinessFinanceInstitutions { get; } = new List<UserBusinessFinanceInstitution>();

    public virtual ICollection<UserBusinessKeyDocument> UserBusinessKeyDocuments { get; } = new List<UserBusinessKeyDocument>();

    public virtual ICollection<UserBusinessSaasAppCredential> UserBusinessSaasAppCredentials { get; } = new List<UserBusinessSaasAppCredential>();

    public virtual ICollection<UserBusinessUserMap> UserBusinessUserMaps { get; } = new List<UserBusinessUserMap>();
}
