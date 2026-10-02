using FluentAssertions;
using Moq;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.DTOs.Roles;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;
using Xunit;

namespace SportsCenterManagement.Tests;

/// <summary>
/// Minh chứng Unit Test chuẩn 3 tầng: Dùng Moq giả lập tầng DAL và FluentAssertions để kiểm tra kết quả.
/// </summary>
public class BLLUnitTestsWithMoq
{
    [Fact]
    public async Task GetAllPermissionsAsync_ShouldReturnOrderedPermissions_UsingMoq()
    {
        // 1. Arrange: Dùng Moq để giả lập Repository và Unit of Work (không phụ thuộc vào DB thật)
        var mockPermissionRepo = new Mock<IGenericRepository<Permission>>();
        var fakePermissions = new List<Permission>
        {
            new() { Id = 1, Code = "ROLE_MANAGE", Name = "Quản lý vai trò", Description = "Phân quyền" },
            new() { Id = 2, Code = "CLASS_VIEW", Name = "Xem lớp học", Description = "Danh mục lớp" },
            new() { Id = 3, Code = "MEMBER_VIEW", Name = "Xem hội viên", Description = "Hồ sơ hội viên" }
        };

        mockPermissionRepo
            .Setup(repo => repo.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakePermissions);

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        mockUnitOfWork
            .Setup(uow => uow.Repository<Permission>())
            .Returns(mockPermissionRepo.Object);

        var service = new RolePermissionService(mockUnitOfWork.Object);

        // 2. Act: Thực thi phương thức nghiệp vụ của BLL
        var result = await service.GetAllPermissionsAsync(CancellationToken.None);

        // 3. Assert: Dùng FluentAssertions để kiểm tra kết quả rõ ràng, mạch lạc
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().BeInAscendingOrder(p => p.Code);
        result[0].Code.Should().Be("CLASS_VIEW");
        result[1].Code.Should().Be("MEMBER_VIEW");
        result[2].Code.Should().Be("ROLE_MANAGE");

        // Xác nhận repository được gọi đúng 1 lần
        mockPermissionRepo.Verify(repo => repo.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-99)]
    public async Task GetActivePackagesAsync_ShouldThrowKeyNotFound_WhenCenterIdIsInvalid(long invalidCenterId)
    {
        // 1. Arrange
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var service = new MembershipPackageService(mockUnitOfWork.Object);

        // 2. Act
        Func<Task> act = async () => await service.GetActivePackagesAsync(invalidCenterId, CancellationToken.None);

        // 3. Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Không tìm thấy trung tâm đang hoạt động.");
    }

    [Fact]
    public void PasswordHashing_ShouldGenerateValidBcryptHash_AndVerifyCorrectly()
    {
        // Arrange
        const string rawPassword = "StrongPassword@2026!";

        // Act
        var hash = PasswordHashing.Hash(rawPassword);

        // Assert: FluentAssertions
        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().StartWith("$2"); // BCrypt signature

        PasswordHashing.Verify(rawPassword, hash).Should().BeTrue();
        PasswordHashing.Verify("WrongPassword!123", hash).Should().BeFalse();
    }
}
