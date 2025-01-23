using EstateKit.Core.Models.UserModels;
using EstateKit.Core.Models.UserModels.Business;
using EstateKit.Core.Models.ContactModels;

namespace EstateKit.Core.Models.Common;

/// <summary>
/// Contains a list of addresses
/// </summary>
public partial class Address
{
    public long Id { get; set; }

    public string Line1 { get; set; } = null!;

    public string? Line2 { get; set; }

    public string? City { get; set; }

    public long ProvStateId { get; set; }

    public long CountryId { get; set; }

    public string PostalZip { get; set; } = null!;

    public string? AddressName { get; set; }

    public bool Active { get; set; }

    public virtual ICollection<AccessInfo> AccessInfos { get; } = new List<AccessInfo>();

    public virtual ICollection<Company> Companies { get; } = new List<Company>();

    public virtual ICollection<ContactAddress> ContactAddresses { get; } = new List<ContactAddress>();

    public virtual Country Country { get; set; } = null!;

    public virtual ProvState ProvState { get; set; } = null!;

    public virtual ICollection<UserAsset> UserAssets { get; } = new List<UserAsset>();

    public virtual ICollection<UserBusinessAddress> UserBusinessAddresses { get; } = new List<UserBusinessAddress>();

    public virtual ICollection<UserBusiness> UserBusinesses { get; } = new List<UserBusiness>();

    public virtual ICollection<UserDenomination> UserDenominations { get; } = new List<UserDenomination>();

    public virtual ICollection<User> Users { get; } = new List<User>();
}
