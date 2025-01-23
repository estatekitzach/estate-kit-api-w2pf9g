using EstateKit.Core.Models.Common;
using EstateKit.Core.Models.UserModels;
using EstateKit.Core.Models.UserModels.Business;
using EstateKit.Core.Models.UserModels.Medical;

namespace EstateKit.Core.Models.ContactModels;

/// <summary>
/// The primary table for a contact
/// </summary>
public partial class Contact
{
    public long Id { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string? MiddleName { get; set; }

    public string? MaidenName { get; set; }

    public bool? Active { get; set; }

    public virtual ICollection<ContactAddress> ContactAddresses { get; } = new List<ContactAddress>();

    public virtual ICollection<ContactCitizenship> ContactCitizenships { get; } = new List<ContactCitizenship>();

    public virtual ICollection<ContactContactMethod> ContactContactMethods { get; } = new List<ContactContactMethod>();

    public virtual ICollection<ContactEducation> ContactEducations { get; } = new List<ContactEducation>();

    public virtual ICollection<ContactRelationship> ContactRelationshipContacts { get; } = new List<ContactRelationship>();

    public virtual ICollection<ContactRelationship> ContactRelationshipRelatedContacts { get; } = new List<ContactRelationship>();

    public virtual ICollection<DigitalAsset> DigitalAssets { get; } = new List<DigitalAsset>();

    public virtual ICollection<UserBusinessEmployee> UserBusinessEmployees { get; } = new List<UserBusinessEmployee>();

    public virtual ICollection<UserBusinessFinanceInstitution> UserBusinessFinanceInstitutions { get; } = new List<UserBusinessFinanceInstitution>();

    public virtual ICollection<UserBusinessVendor> UserBusinessVendors { get; } = new List<UserBusinessVendor>();

    public virtual ICollection<UserContactRelationship> UserContactRelationships { get; } = new List<UserContactRelationship>();

    public virtual ICollection<UserFuneral> UserFunerals { get; } = new List<UserFuneral>();

    public virtual ICollection<UserInsurance> UserInsurances { get; } = new List<UserInsurance>();

    public virtual ICollection<UserMedical> UserMedicalHealthCareRep1Contacts { get; } = new List<UserMedical>();

    public virtual ICollection<UserMedical> UserMedicalHealthCareRep2Contacts { get; } = new List<UserMedical>();

    public virtual ICollection<UserMedical> UserMedicalPhoneContacts { get; } = new List<UserMedical>();

    public virtual ICollection<UserMedical> UserMedicalPrimaryPhysicianContacts { get; } = new List<UserMedical>();

    public virtual ICollection<UserMedicalRelationship> UserMedicalRelationships { get; } = new List<UserMedicalRelationship>();

    public virtual ICollection<UserPolicy> UserPolicies { get; } = new List<UserPolicy>();

    public virtual ICollection<User> Users { get; } = new List<User>();
}
