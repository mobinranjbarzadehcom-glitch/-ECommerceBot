using ECommerceBot.API.DTOs.Ticket;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Repositories.Interfaces;
using ECommerceBot.API.Services.Implementations;
using ECommerceBot.API.UnitOfWork;
using Moq;
using Xunit;

namespace ECommerceBot.Tests.Services;

public class TicketServiceTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<ITicketRepository> _ticketRepoMock;
    private readonly Mock<ITicketMessageRepository> _ticketMessageRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly TicketService _sut;

    public TicketServiceTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _ticketRepoMock = new Mock<ITicketRepository>();
        _ticketMessageRepoMock = new Mock<ITicketMessageRepository>();
        _userRepoMock = new Mock<IUserRepository>();

        _uowMock.Setup(u => u.Tickets).Returns(_ticketRepoMock.Object);
        _uowMock.Setup(u => u.TicketMessages).Returns(_ticketMessageRepoMock.Object);
        _uowMock.Setup(u => u.Users).Returns(_userRepoMock.Object);

        _sut = new TicketService(_uowMock.Object);
    }

    [Fact]
    public async Task CreateTicketAsync_WithEmptySubject_ReturnsFailure()
    {
        var dto = new CreateTicketDto { Subject = string.Empty, Message = "Hello" };

        var result = await _sut.CreateTicketAsync(1, dto);

        Assert.False(result.IsSuccess);
        Assert.Contains("subject", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateTicketAsync_WithEmptyMessage_ReturnsFailure()
    {
        var dto = new CreateTicketDto { Subject = "Issue", Message = string.Empty };

        var result = await _sut.CreateTicketAsync(1, dto);

        Assert.False(result.IsSuccess);
        Assert.Contains("message", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateTicketAsync_WhenUserNotFound_ReturnsFailure()
    {
        var dto = new CreateTicketDto { Subject = "Help", Message = "I need help" };
        _userRepoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((TelegramUser?)null);

        var result = await _sut.CreateTicketAsync(99, dto);

        Assert.False(result.IsSuccess);
        Assert.Equal("User not found", result.ErrorMessage);
    }

    [Fact]
    public async Task ReplyTicketAsync_WithEmptyMessage_ReturnsFailure()
    {
        var dto = new ReplyTicketDto { Message = string.Empty };

        var result = await _sut.ReplyTicketAsync(1, 1, dto, false);

        Assert.False(result.IsSuccess);
        Assert.Contains("message", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReplyTicketAsync_WhenTicketNotFound_ReturnsFailure()
    {
        var dto = new ReplyTicketDto { Message = "Test reply" };
        _ticketRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Ticket?)null);

        var result = await _sut.ReplyTicketAsync(999, 1, dto, false);

        Assert.False(result.IsSuccess);
        Assert.Equal("Ticket not found", result.ErrorMessage);
    }

    [Fact]
    public async Task ReplyTicketAsync_WhenTicketResolved_ReturnsFailure()
    {
        var dto = new ReplyTicketDto { Message = "Test reply" };
        var ticket = new Ticket { Id = 1, Status = TicketStatus.Resolved };
        _ticketRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(ticket);

        var result = await _sut.ReplyTicketAsync(1, 1, dto, false);

        Assert.False(result.IsSuccess);
        Assert.Contains("closed or resolved", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReplyTicketAsync_AdminReply_SetsStatusToInProgress()
    {
        var dto = new ReplyTicketDto { Message = "Admin reply" };
        var ticket = new Ticket { Id = 1, Status = TicketStatus.Open };
        _ticketRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(ticket);
        _ticketMessageRepoMock.Setup(r => r.AddAsync(It.IsAny<TicketMessage>())).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _sut.ReplyTicketAsync(1, 10, dto, isAdmin: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Equal(10, ticket.AssignedAdminId);
    }

    [Fact]
    public async Task ResolveTicketAsync_WhenTicketNotFound_ReturnsFailure()
    {
        _ticketRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Ticket?)null);

        var result = await _sut.ResolveTicketAsync(999, 1);

        Assert.False(result.IsSuccess);
        Assert.Equal("Ticket not found", result.ErrorMessage);
    }

    [Fact]
    public async Task ResolveTicketAsync_WhenOpen_SetsStatusToResolved()
    {
        var ticket = new Ticket { Id = 1, Status = TicketStatus.Open };
        _ticketRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(ticket);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _sut.ResolveTicketAsync(1, 5);

        Assert.True(result.IsSuccess);
        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(5, ticket.AssignedAdminId);
    }
}
