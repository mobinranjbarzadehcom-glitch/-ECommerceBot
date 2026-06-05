using ECommerceBot.API.Entities;
using ECommerceBot.API.Infrastructure.Audit;
using ECommerceBot.API.Repositories.Interfaces;
using ECommerceBot.API.UnitOfWork;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ECommerceBot.Tests.Services;

public class AuditLogServiceTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IAuditLogRepository> _auditRepoMock;
    private readonly AuditLogService _sut;

    public AuditLogServiceTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _auditRepoMock = new Mock<IAuditLogRepository>();

        _uowMock.Setup(u => u.AuditLogs).Returns(_auditRepoMock.Object);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        _sut = new AuditLogService(_uowMock.Object, NullLogger<AuditLogService>.Instance);
    }

    [Fact]
    public async Task LogAsync_WithValidData_SavesAuditLog()
    {
        _auditRepoMock.Setup(r => r.AddAsync(It.IsAny<AuditLog>())).Returns(Task.CompletedTask);

        await _sut.LogAsync(1, AuditAction.ApproveOrder, "Order", 42, "Test details");

        _auditRepoMock.Verify(r => r.AddAsync(It.Is<AuditLog>(a =>
            a.AdminId == 1 &&
            a.Action == AuditAction.ApproveOrder &&
            a.TargetType == "Order" &&
            a.TargetId == 42 &&
            a.Details == "Test details")), Times.Once);

        _uowMock.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task LogAsync_WhenSaveFails_DoesNotThrow()
    {
        _auditRepoMock.Setup(r => r.AddAsync(It.IsAny<AuditLog>())).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ThrowsAsync(new Exception("DB error"));

        // Should swallow the exception — audit log failure must not break the caller
        var exception = await Record.ExceptionAsync(() =>
            _sut.LogAsync(1, AuditAction.BlockUser, "User", 5));

        Assert.Null(exception);
    }

    [Fact]
    public async Task LogAsync_WithNullTargetType_SavesWithNulls()
    {
        _auditRepoMock.Setup(r => r.AddAsync(It.IsAny<AuditLog>())).Returns(Task.CompletedTask);

        await _sut.LogAsync(2, AuditAction.EditSetting);

        _auditRepoMock.Verify(r => r.AddAsync(It.Is<AuditLog>(a =>
            a.AdminId == 2 &&
            a.Action == AuditAction.EditSetting &&
            a.TargetType == null &&
            a.TargetId == null)), Times.Once);
    }

    [Fact]
    public async Task GetRecentAsync_DelegatesToRepository()
    {
        var expected = new List<AuditLog> { new AuditLog { Action = "Test" } };
        _auditRepoMock.Setup(r => r.GetRecentAsync(50)).ReturnsAsync(expected);

        var result = await _sut.GetRecentAsync(50);

        Assert.Single(result);
        _auditRepoMock.Verify(r => r.GetRecentAsync(50), Times.Once);
    }

    [Fact]
    public async Task GetByAdminAsync_DelegatesToRepository()
    {
        var expected = new List<AuditLog> { new AuditLog { AdminId = 7 } };
        _auditRepoMock.Setup(r => r.GetByAdminIdAsync(7, 25)).ReturnsAsync(expected);

        var result = await _sut.GetByAdminAsync(7, 25);

        Assert.Single(result);
    }
}
