using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("ai_messages")]
public class AiMessage : Common.BaseEntity
{
    [Column("conversation_id")]
    public long ConversationId { get; set; }

    [MaxLength(30)]
    [Column("sender_type")]
    public string SenderType { get; set; } = null!;

    [Column("message")]
    public string Message { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}
