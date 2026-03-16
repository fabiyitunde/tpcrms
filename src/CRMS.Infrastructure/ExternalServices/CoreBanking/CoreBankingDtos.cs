using System.Text.Json.Serialization;

namespace CRMS.Infrastructure.ExternalServices.CoreBanking;

// GET /core/account/fulldetailsbynuban/{nuban}
public class FullDetailsByNubanResponse
{
    [JsonPropertyName("clientType")]
    public string? ClientType { get; set; }

    [JsonPropertyName("clientDetails")]
    public CbsClientDetails? ClientDetails { get; set; }

    [JsonPropertyName("directors")]
    public List<CbsPartyDetails>? Directors { get; set; }

    [JsonPropertyName("signatories")]
    public List<CbsPartyDetails>? Signatories { get; set; }
}

public class CbsClientDetails
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("fullName")]
    public string? FullName { get; set; }

    [JsonPropertyName("mobileNo")]
    public string? MobileNo { get; set; }

    [JsonPropertyName("bvn")]
    public string? Bvn { get; set; }

    [JsonPropertyName("officeId")]
    public int OfficeId { get; set; }

    [JsonPropertyName("officeName")]
    public string? OfficeName { get; set; }

    [JsonPropertyName("clientType")]
    public string? ClientType { get; set; }

    [JsonPropertyName("incorporationNumber")]
    public string? IncorporationNumber { get; set; }

    [JsonPropertyName("kycLevel")]
    public int KycLevel { get; set; }

    [JsonPropertyName("emailNotification")]
    public bool EmailNotification { get; set; }

    [JsonPropertyName("smsNotification")]
    public bool SmsNotification { get; set; }

    [JsonPropertyName("isStaff")]
    public bool IsStaff { get; set; }

    [JsonPropertyName("activationDate")]
    public string? ActivationDate { get; set; }

    [JsonPropertyName("dateOfBirth")]
    public string? DateOfBirth { get; set; }

    [JsonPropertyName("addressType")]
    public string? AddressType { get; set; }

    [JsonPropertyName("addressLine1")]
    public string? AddressLine1 { get; set; }

    [JsonPropertyName("addressLine2")]
    public string? AddressLine2 { get; set; }

    [JsonPropertyName("addressLine3")]
    public string? AddressLine3 { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; set; }

    [JsonPropertyName("submittedOnDate")]
    public string? SubmittedOnDate { get; set; }

    [JsonPropertyName("activatedOnDate")]
    public string? ActivatedOnDate { get; set; }
}

// Directors and Signatories share the same shape in the CBS API
public class CbsPartyDetails
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("firstname")]
    public string? Firstname { get; set; }

    [JsonPropertyName("lastname")]
    public string? Lastname { get; set; }

    [JsonPropertyName("mobileNo")]
    public string? MobileNo { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("bvn")]
    public string? Bvn { get; set; }

    [JsonPropertyName("officeId")]
    public int OfficeId { get; set; }

    [JsonPropertyName("officeName")]
    public string? OfficeName { get; set; }

    [JsonPropertyName("savingsAccountId")]
    public int? SavingsAccountId { get; set; }

    [JsonPropertyName("clientType")]
    public string? ClientType { get; set; }

    [JsonPropertyName("kycLevel")]
    public int KycLevel { get; set; }

    [JsonPropertyName("emailNotification")]
    public bool EmailNotification { get; set; }

    [JsonPropertyName("smsNotification")]
    public bool SmsNotification { get; set; }

    [JsonPropertyName("isStaff")]
    public bool IsStaff { get; set; }

    [JsonPropertyName("activationDate")]
    public string? ActivationDate { get; set; }

    [JsonPropertyName("dateOfBirth")]
    public string? DateOfBirth { get; set; }

    [JsonPropertyName("addressType")]
    public string? AddressType { get; set; }

    [JsonPropertyName("addressLine1")]
    public string? AddressLine1 { get; set; }

    [JsonPropertyName("addressLine2")]
    public string? AddressLine2 { get; set; }

    [JsonPropertyName("addressLine3")]
    public string? AddressLine3 { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; set; }

    public string FullName => $"{Firstname} {Lastname}".Trim();

    public string? Address
    {
        get
        {
            var parts = new[] { AddressLine1, AddressLine2, AddressLine3, City, State }
                .Where(p => !string.IsNullOrWhiteSpace(p));
            return parts.Any() ? string.Join(", ", parts) : null;
        }
    }
}

// GET /core/transactions/{nuban}?startDate=DD-MM-YYYY&endDate=DD-MM-YYYY
public class CbsTransaction
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("nuban")]
    public string? Nuban { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("transactionType")]
    public string? TransactionType { get; set; }

    [JsonPropertyName("balance")]
    public decimal Balance { get; set; }

    [JsonPropertyName("createdDate")]
    public DateTime CreatedDate { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("clientName")]
    public string? ClientName { get; set; }

    [JsonPropertyName("sessionID")]
    public string? SessionId { get; set; }

    [JsonPropertyName("transactionReference")]
    public string? TransactionReference { get; set; }

    [JsonPropertyName("transferDetails")]
    public CbsTransferDetails? TransferDetails { get; set; }
}

public class CbsTransferDetails
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("transactionStatus")]
    public string? TransactionStatus { get; set; }

    [JsonPropertyName("narration")]
    public string? Narration { get; set; }

    [JsonPropertyName("sourceAccountNUBAN")]
    public string? SourceAccountNuban { get; set; }

    [JsonPropertyName("sourceAccountName")]
    public string? SourceAccountName { get; set; }

    [JsonPropertyName("destinationBankName")]
    public string? DestinationBankName { get; set; }

    [JsonPropertyName("destinationAccountName")]
    public string? DestinationAccountName { get; set; }

    [JsonPropertyName("destinationAccountNUBAN")]
    public string? DestinationAccountNuban { get; set; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }
}

// OAuth2 token response
public class CbsOAuth2TokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("accessToken")]
    public string? AccessTokenAlt { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("data")]
    public CbsTokenData? Data { get; set; }

    public string? ResolvedToken =>
        AccessToken ?? AccessTokenAlt ?? Token ?? Data?.Token ?? Data?.AccessToken;
}

public class CbsTokenData
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }
}
