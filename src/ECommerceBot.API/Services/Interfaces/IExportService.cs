namespace ECommerceBot.API.Services.Interfaces;

public interface IExportService
{
    /// <summary>Returns all orders as a UTF-8 CSV byte array.</summary>
    Task<byte[]> ExportOrdersCsvAsync();

    /// <summary>Returns all users as a UTF-8 CSV byte array.</summary>
    Task<byte[]> ExportUsersCsvAsync();
}
