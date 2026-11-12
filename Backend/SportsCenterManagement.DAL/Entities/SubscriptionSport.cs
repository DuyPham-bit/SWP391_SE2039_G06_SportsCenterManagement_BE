using System.ComponentModel.DataAnnotations.Schema;

namespace SportsCenterManagement.DAL.Entities;

/// <summary>Sports a member picked for a non all-access subscription.</summary>
[Table("subscription_sports")]
public class SubscriptionSport
{
    [Column("subscription_id")]
    public long SubscriptionId { get; set; }

    [Column("sport_id")]
    public long SportId { get; set; }
}
