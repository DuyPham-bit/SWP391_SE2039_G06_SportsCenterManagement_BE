using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class CoachAssignmentQueryService(IUnitOfWork unitOfWork) : ICoachAssignmentQueryService
{
    public async Task<IReadOnlyList<CoachAssignmentCandidateResponse>> GetCandidatesAsync(
        long centerId,
        CancellationToken cancellationToken = default)
    {
        var db = unitOfWork.Context;
        var coaches = await (from coach in db.CoachProfiles.AsNoTracking()
            join user in db.Users.AsNoTracking() on coach.UserId equals user.Id
            where coach.CenterId == centerId && coach.Status == "Active" && user.Status == "Active"
            orderby coach.FullName
            select new
            {
                coach.Id,
                coach.UserId,
                coach.CoachCode,
                coach.FullName,
                coach.Specialization,
                coach.Certification
            }).ToListAsync(cancellationToken);

        if (coaches.Count == 0) return [];

        var coachIds = coaches.Select(coach => coach.Id).ToArray();
        var sportLinks = await db.CoachSports.AsNoTracking()
            .Where(link => coachIds.Contains(link.CoachId))
            .Select(link => new { link.CoachId, link.SportId })
            .ToListAsync(cancellationToken);

        var today = ScheduleTime.VietnamToday;
        var slots = await (from assignment in db.ClassCoaches.AsNoTracking()
            join cls in db.Classes.AsNoTracking() on assignment.ClassId equals cls.Id
            join schedule in db.ClassSchedules.AsNoTracking() on cls.Id equals schedule.ClassId
            join room in db.Rooms.AsNoTracking() on schedule.RoomId equals room.Id into roomRows
            from room in roomRows.DefaultIfEmpty()
            where coachIds.Contains(assignment.CoachId)
                && cls.CenterId == centerId
                && cls.Status != "Cancelled" && cls.Status != "Completed"
                && schedule.Status == "Active"
                && (!schedule.EndDate.HasValue || schedule.EndDate.Value >= today)
            orderby schedule.DayOfWeek, schedule.StartTime
            select new
            {
                assignment.CoachId,
                Slot = new CoachTeachingSlotResponse(
                    cls.Id, cls.Name, schedule.DayOfWeek, schedule.StartTime, schedule.EndTime,
                    schedule.StartDate, schedule.EndDate, room == null ? null : room.Name)
            }).ToListAsync(cancellationToken);

        return coaches.Select(coach => new CoachAssignmentCandidateResponse(
            coach.Id,
            coach.UserId,
            coach.CoachCode,
            coach.FullName,
            coach.Specialization,
            coach.Certification,
            sportLinks.Where(link => link.CoachId == coach.Id).Select(link => link.SportId).ToArray(),
            slots.Where(item => item.CoachId == coach.Id).Select(item => item.Slot).ToArray()
        )).ToArray();
    }
}
