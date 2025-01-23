using EstateKit.Core.Models.ContactModels;
using EstateKit.Core.Models.UserModels;

namespace EstateKit.Core.Models.Common;

/// <summary>
/// Lists the countries 
/// </summary>
public partial class Country
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string CountryCode { get; set; } = null!;

    public bool Active { get; set; }

    public virtual ICollection<Address> Addresses { get; } = new List<Address>();

    public virtual ICollection<ContactCitizenship> ContactCitizenships { get; } = new List<ContactCitizenship>();

    public virtual ICollection<ProvState> ProvStates { get;} = new List<ProvState>();

    public virtual ICollection<UserCivilService> UserCivilServices { get;} = new List<UserCivilService>();
}
