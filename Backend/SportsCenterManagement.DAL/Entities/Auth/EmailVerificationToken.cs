using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("email_verification_tokens")]
public class EmailVerificationToken : BaseEntity
{
    [Column("email"), MaxLength(150)]
    public string Email { get; set; } = null!;

    [Column("token"), MaxLength(255)]
    public string Token { get; set; } = null!;

    [Column("failed_attempts")]
    public int FailedAttempts { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("used_at")]
    public DateTime? UsedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
