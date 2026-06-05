namespace ECommerceBot.API.Enums;

public enum LicenseStatus
{
    Unknown = 0,
    Valid = 1,
    Trial = 2,
    Expired = 3,
    Invalid = 4,
    Disabled = 5,
    NotActivated = 6,
    GracePeriod = 7,
    BotMismatch = 8,
    DomainMismatch = 9,
    ServerMismatch = 10,
    UserLimitExceeded = 11,
    AdminLimitExceeded = 12,
    SignatureInvalid = 13
}
