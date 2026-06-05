using ECommerceBot.API.DTOs.Common;
using ECommerceBot.API.DTOs.Ticket;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Services.Common;
using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.UnitOfWork;

namespace ECommerceBot.API.Services.Implementations;

public class TicketService : ITicketService
{
    private readonly IUnitOfWork _uow;

    public TicketService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ServiceResult<TicketDto>> CreateTicketAsync(int userId, CreateTicketDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Subject))
            return ServiceResult<TicketDto>.Failure("Ticket subject is required");

        if (string.IsNullOrWhiteSpace(dto.Message))
            return ServiceResult<TicketDto>.Failure("Ticket message is required");

        var user = await _uow.Users.GetByIdAsync(userId);
        if (user is null)
            return ServiceResult<TicketDto>.Failure("User not found");

        await _uow.BeginTransactionAsync();
        try
        {
            var ticket = new Ticket
            {
                UserId = userId,
                Subject = dto.Subject,
                Status = TicketStatus.Open,
                RelatedOrderId = dto.RelatedOrderId
            };
            await _uow.Tickets.AddAsync(ticket);
            await _uow.SaveChangesAsync();

            var message = new TicketMessage
            {
                TicketId = ticket.Id,
                SenderId = userId,
                Content = dto.Message,
                IsAdminMessage = false
            };
            await _uow.TicketMessages.AddAsync(message);
            await _uow.SaveChangesAsync();
            await _uow.CommitTransactionAsync();

            var result = await _uow.Tickets.GetTicketWithMessagesAsync(ticket.Id);
            return ServiceResult<TicketDto>.Success(MapToDto(result!));
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<ServiceResult> ReplyTicketAsync(int ticketId, int senderId, ReplyTicketDto dto, bool isAdmin)
    {
        if (string.IsNullOrWhiteSpace(dto.Message))
            return ServiceResult.Failure("Reply message is required");

        var ticket = await _uow.Tickets.GetByIdAsync(ticketId);
        if (ticket is null)
            return ServiceResult.Failure("Ticket not found");

        if (ticket.Status == TicketStatus.Resolved || ticket.Status == TicketStatus.Closed)
            return ServiceResult.Failure("Cannot reply to a closed or resolved ticket");

        var message = new TicketMessage
        {
            TicketId = ticketId,
            SenderId = senderId,
            Content = dto.Message,
            IsAdminMessage = isAdmin
        };
        await _uow.TicketMessages.AddAsync(message);

        if (isAdmin && ticket.Status == TicketStatus.Open)
        {
            ticket.Status = TicketStatus.InProgress;
            ticket.AssignedAdminId = senderId;
            _uow.Tickets.Update(ticket);
        }

        await _uow.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ResolveTicketAsync(int ticketId, int adminId)
    {
        var ticket = await _uow.Tickets.GetByIdAsync(ticketId);
        if (ticket is null)
            return ServiceResult.Failure("Ticket not found");

        if (ticket.Status == TicketStatus.Resolved || ticket.Status == TicketStatus.Closed)
            return ServiceResult.Failure("Ticket is already resolved or closed");

        ticket.Status = TicketStatus.Resolved;
        ticket.AssignedAdminId = adminId;
        _uow.Tickets.Update(ticket);
        await _uow.SaveChangesAsync();

        return ServiceResult.Success();
    }

    public async Task<ServiceResult<TicketDto>> GetTicketByIdAsync(int ticketId)
    {
        var ticket = await _uow.Tickets.GetTicketWithMessagesAsync(ticketId);
        return ticket is null
            ? ServiceResult<TicketDto>.Failure("Ticket not found")
            : ServiceResult<TicketDto>.Success(MapToDto(ticket));
    }

    public async Task<ServiceResult<IEnumerable<TicketDto>>> GetUserTicketsAsync(int userId)
    {
        var tickets = await _uow.Tickets.GetByUserIdAsync(userId);
        return ServiceResult<IEnumerable<TicketDto>>.Success(tickets.Select(MapToDto));
    }

    public async Task<ServiceResult<PagedResultDto<TicketDto>>> GetOpenTicketsAsync(int page, int pageSize)
    {
        var all = (await _uow.Tickets.GetOpenTicketsAsync()).ToList();
        var totalCount = all.Count;
        var items = all
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToDto)
            .ToList();

        return ServiceResult<PagedResultDto<TicketDto>>.Success(new PagedResultDto<TicketDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    private static TicketDto MapToDto(Ticket t) => new()
    {
        Id = t.Id,
        UserId = t.UserId,
        UserName = t.User?.FirstName ?? string.Empty,
        Subject = t.Subject,
        Status = t.Status,
        AssignedAdminId = t.AssignedAdminId,
        RelatedOrderId = t.RelatedOrderId,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        Messages = t.Messages.Select(m => new TicketMessageDto
        {
            Id = m.Id,
            TicketId = m.TicketId,
            SenderId = m.SenderId,
            SenderName = m.Sender?.FirstName ?? string.Empty,
            Content = m.Content,
            IsAdminMessage = m.IsAdminMessage,
            CreatedAt = m.CreatedAt
        }).ToList()
    };
}
