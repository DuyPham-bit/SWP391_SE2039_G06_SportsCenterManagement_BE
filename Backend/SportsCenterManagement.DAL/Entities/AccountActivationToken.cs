using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

/// <summary>
/// Single-use link token that verifies a counter-created member's email and replaces the default password.
/// Only the SHA-256 hash of the token is stored; the raw token exists only in the emailed link.
/// </summary>
[Table("account_activation_tokens")]
public class AccountActivationToken : BaseEntity
{
    [Column("user_id")]
    public long UserId { get; set; }

    [Column("token_hash"), MaxLength(64)]
    public string TokenHash { get; set; } = null!;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("used_at")]
    public DateTime? UsedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
