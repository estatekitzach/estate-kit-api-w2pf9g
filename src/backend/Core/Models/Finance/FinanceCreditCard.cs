using EstateKit.Core.Models.UserModels.Business;  

namespace EstateKit.Core.Models.Finance;

public partial class FinanceCreditCard
{
    public long Id { get; set; }

    public long CreditCardTypeId { get; set; }

    public string NameOnCard { get; set; } = null!;

    public string ExpirationDate { get; set; } = null!;

    public string Cvc { get; set; } = null!;

    public string? OnlineUsername { get; set; }

    public string? OnlinePassword { get; set; }

    public string? CardLocation { get; set; }

    public string? Notes { get; set; }

    public bool Active { get; set; }

    public virtual EstateKit.Core.Models.Common.Type CreditCardType { get; set; } = null!;

    public virtual ICollection<UserBusinessCreditCard> UserBusinessCreditCards { get; } = new List<UserBusinessCreditCard>();
}
