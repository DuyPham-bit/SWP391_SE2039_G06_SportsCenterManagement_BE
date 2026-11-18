using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("support_request_messages")]
public class SupportRequestMessage : Common.BaseEntity
{
    [Column("request_id")]
    public long RequestId { get; set; }

    [Column("sender_id")]
    public long SenderId { get; set; }

    [Column("message")]
    public string Message { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}
