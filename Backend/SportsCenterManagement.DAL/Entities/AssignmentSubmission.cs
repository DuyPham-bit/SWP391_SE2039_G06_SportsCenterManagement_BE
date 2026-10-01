using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("assignment_submissions")]
public class AssignmentSubmission : Common.BaseEntity
{
    [Column("assignment_id")]
    public long AssignmentId { get; set; }

    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("submitted_at")]
    public DateTime? SubmittedAt { get; set; }

    [Column("content")]
    public string? Content { get; set; }

    [Precision(5, 2)]
    [Column("score")]
    public decimal? Score { get; set; }

    [MaxLength(1000)]
    [Column("coach_feedback")]
    public string? CoachFeedback { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

}
