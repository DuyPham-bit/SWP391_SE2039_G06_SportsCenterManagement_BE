using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("ai_conversations")]
public class AiConversation : Common.BaseEntity
{
    [Column("user_id")]
    public long UserId { get; set; }

    [Column("member_id")]
    public long? MemberId { get; set; }

    [MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = null!;

    [MaxLength(50)]
    [Column("conversation_type")]
    public string ConversationType { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

}
