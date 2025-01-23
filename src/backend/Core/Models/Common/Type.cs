
using EstateKit.Core.Models.Finance;
using EstateKit.Core.Models.UserModels.Business;
using EstateKit.Core.Models.ContactModels;
using EstateKit.Core.Models.UserModels;
using EstateKit.Core.Models.UserModels.Medical;

namespace EstateKit.Core.Models.Common;

/// <summary>
/// Lists the types available. i.e. medical practitioner types
/// </summary>
public partial class Type
{
    public long Id { get; set; }

    public long TypeGroupId { get; set; }

    public string? Key { get; set; }

    public string Name { get; set; } = null!;

    public bool Active { get; set; }

    public virtual ICollection<BusinessContactMethod> BusinessContactMethods { get; } = new List<BusinessContactMethod>();

    public virtual ICollection<ContactAddress> ContactAddresses { get; } = new List<ContactAddress>();

    public virtual ICollection<ContactCitizenship> ContactCitizenships { get; } = new List<ContactCitizenship>();

    public virtual ICollection<ContactContactMethod> ContactContactMethods { get; } = new List<ContactContactMethod>();

    public virtual ICollection<ContactRelationship> ContactRelationships { get; } = new List<ContactRelationship>();

    public virtual ICollection<DigitalAsset> DigitalAssets { get; } = new List<DigitalAsset>();

    public virtual ICollection<DocumentIdentifierMap> DocumentIdentifierMapIdentifierTypes { get; } = new List<DocumentIdentifierMap>();

    public virtual ICollection<DocumentIdentifierMap> DocumentIdentifierMapUserDocumentTypes { get; } = new List<DocumentIdentifierMap>();

    public virtual ICollection<DocumentTypeProvStateLink> DocumentTypeProvStateLinks { get; } = new List<DocumentTypeProvStateLink>();

    public virtual ICollection<FinanceAccount> FinanceAccounts { get; } = new List<FinanceAccount>();

    public virtual ICollection<FinanceCreditCard> FinanceCreditCards { get; } = new List<FinanceCreditCard>();

    public virtual ICollection<ReligiousDenomination> ReligiousDenominations { get; } = new List<ReligiousDenomination>();

    public virtual TypeGroup TypeGroup { get; set; } = null!;

    public virtual ICollection<UserAsset> UserAssets { get; } = new List<UserAsset>();

    public virtual ICollection<UserBusinessAsset> UserBusinessAssets { get; } = new List<UserBusinessAsset>();

    public virtual ICollection<UserBusiness> UserBusinessBusinessInterestTypes { get; } = new List<UserBusiness>();

    public virtual ICollection<UserBusiness> UserBusinessDispositionWishTypes { get; } = new List<UserBusiness>();

    public virtual ICollection<UserBusinessEmployee> UserBusinessEmployees { get; } = new List<UserBusinessEmployee>();

    public virtual ICollection<UserBusinessKeyDocument> UserBusinessKeyDocuments { get; } = new List<UserBusinessKeyDocument>();

    public virtual ICollection<UserBusiness> UserBusinessOwnershipTypes { get; } = new List<UserBusiness>();

    public virtual ICollection<UserBusinessVendor> UserBusinessVendors { get; } = new List<UserBusinessVendor>();

    public virtual ICollection<UserContactRelationship> UserContactRelationships { get; } = new List<UserContactRelationship>();

    public virtual ICollection<UserDocument> UserDocuments { get; } = new List<UserDocument>();

    public virtual ICollection<UserFuneral> UserFuneralBodyDisposalTypes { get; } = new List<UserFuneral>();

    public virtual ICollection<UserFuneral> UserFuneralCasketPreferenceTypes { get; } = new List<UserFuneral>();

    public virtual ICollection<UserFuneral> UserFuneralFuneralBudgetTypes { get; } = new List<UserFuneral>();

    public virtual ICollection<UserIdentifier> UserIdentifiers { get; } = new List<UserIdentifier>();

    public virtual ICollection<UserInsurance> UserInsurances { get; } = new List<UserInsurance>();

    public virtual ICollection<UserMedicalDocument> UserMedicalDocuments { get; } = new List<UserMedicalDocument>();

    public virtual ICollection<UserMedicalRelationship> UserMedicalRelationships { get; } = new List<UserMedicalRelationship>();

    public virtual ICollection<UserPolicy> UserPolicies { get; } = new List<UserPolicy>();

    public virtual ICollection<UserRelationship> UserRelationships { get; } = new List<UserRelationship>();

    public virtual ICollection<User> Users { get; } = new List<User>();
}
