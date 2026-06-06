using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.UnitOfWork;
using System.Text;

namespace ECommerceBot.API.Services.Implementations;

public class ExportService : IExportService
{
    private readonly IUnitOfWork _uow;

    public ExportService(IUnitOfWork uow) => _uow = uow;

    public async Task<byte[]> ExportOrdersCsvAsync()
    {
        var orders = await _uow.Orders.GetAllAsync();
        var sb = new StringBuilder();
        sb.AppendLine("Id,UserId,TotalAmount,DiscountAmount,Status,PaymentMethod,AccountDetails,CouponId,CreatedAt");

        foreach (var o in orders)
        {
            sb.AppendLine(
                $"{o.Id}," +
                $"{o.UserId}," +
                $"{o.TotalAmount:F2}," +
                $"{o.DiscountAmount:F2}," +
                $"{o.Status}," +
                $"\"{EscapeCsv(o.AccountDetails)}\"," +
                $"{(o.CouponId.HasValue ? o.CouponId.Value.ToString() : "")}," +
                $"{o.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> ExportUsersCsvAsync()
    {
        var users = await _uow.Users.GetAllAsync();
        var sb = new StringBuilder();
        sb.AppendLine("Id,TelegramId,FirstName,LastName,Username,Role,WalletBalance,IsBlocked,CreatedAt");

        foreach (var u in users)
        {
            sb.AppendLine(
                $"{u.Id}," +
                $"{u.TelegramId}," +
                $"\"{EscapeCsv(u.FirstName)}\"," +
                $"\"{EscapeCsv(u.LastName)}\"," +
                $"\"{EscapeCsv(u.Username)}\"," +
                $"{u.Role}," +
                $"{u.WalletBalance:F2}," +
                $"{u.IsBlocked}," +
                $"{u.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string EscapeCsv(string? value) =>
        value?.Replace("\"", "\"\"") ?? string.Empty;
}
