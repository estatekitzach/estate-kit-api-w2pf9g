using EstateKit.Core.Models.ContactModels;

namespace EstateKit.Core.Models.Common;

public partial class Company
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public long? AddressId { get; set; }

    public bool Active { get; set; }

    public virtual Address? Address { get; set; }

    public virtual ICollection<ContactCompany> ContactCompanies { get; } = new List<ContactCompany>();
}
