using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("center_checkins")]
public class CenterCheckin : Common.BaseEntity
{
    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("center_id")]
    public long CenterId { get; set; }

    [Column("checked_in_by")]
    public long? CheckedInBy { get; set; }

    [Column("check_in_time")]
    public DateTime CheckInTime { get; set; }

    [Column("check_out_time")]
    public DateTime? CheckOutTime { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}
