namespace ECommerceBot.API.Infrastructure.Security;

public interface IAesEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}
