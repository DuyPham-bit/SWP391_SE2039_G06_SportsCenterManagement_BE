using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Members;

/// <summary>
/// Yêu cầu đăng ký mua gói tập thể thao.
/// </summary>
/// <param name="PackageId">Mã định danh gói tập (ID trong bảng membership_packages).</param>
public sealed record CreateSubscriptionRequest([property: Range(1, long.MaxValue)] long PackageId);
