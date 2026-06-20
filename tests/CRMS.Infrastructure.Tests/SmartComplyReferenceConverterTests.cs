using System.Text.Json;
using CRMS.Infrastructure.ExternalServices.SmartComply;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Regression tests for the production CAC bug:
/// "The JSON value could not be converted to CacCountryReference. Path: $.data.directors[0].countryFk".
/// SmartComply returns FK reference fields (countryFk, affiliateType) sometimes as the embedded
/// object and sometimes as a bare foreign-key id. The lenient converters must accept either shape.
/// </summary>
public class SmartComplyReferenceConverterTests
{
    private static JsonSerializerOptions Opts() => new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new FlexibleDecimalConverter(),
            new FlexibleNullableDecimalConverter(),
            new FlexibleIntConverter(),
            new FlexibleDateTimeConverter(),
            new FlexibleNullableDateTimeConverter(),
            new FlexibleStringConverter(),
            new FlexibleCacCountryReferenceConverter(),
            new FlexibleCacAffiliateTypeReferenceConverter(),
            new TolerantPrimitiveConverterFactory(),
        }
    };

    [Fact]
    public void CountryFk_AsBareNumber_DoesNotThrow()
    {
        var r = JsonSerializer.Deserialize<CacCountryReference>("566", Opts());
        Assert.NotNull(r);
        Assert.Equal(566, r!.Id);
        Assert.Null(r.Name);
    }

    [Fact]
    public void CountryFk_AsObject_StillMapsName()
    {
        var r = JsonSerializer.Deserialize<CacCountryReference>("{\"id\":566,\"code\":\"NG\",\"name\":\"NIGERIA\"}", Opts());
        Assert.Equal(566, r!.Id);
        Assert.Equal("NIGERIA", r.Name);
    }

    [Fact]
    public void CountryFk_AsNull_ReturnsNull()
        => Assert.Null(JsonSerializer.Deserialize<CacCountryReference>("null", Opts()));

    [Fact]
    public void AffiliateType_AsBareNumber_DoesNotThrow()
    {
        var r = JsonSerializer.Deserialize<CacAffiliateTypeReference>("3", Opts());
        Assert.NotNull(r);
        Assert.Equal(3L, r!.Id);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("\"Y\"", true)]
    [InlineData("\"N\"", false)]
    [InlineData("\"true\"", true)]
    [InlineData("\"false\"", false)]
    [InlineData("true", true)]
    [InlineData("null", null)]
    public void Bool_AcceptsNumberStringOrBool(string json, bool? expected)
        => Assert.Equal(expected, JsonSerializer.Deserialize<bool?>(json, Opts()));

    [Fact]
    public void Long_AcceptsQuotedNumber()
        => Assert.Equal(123L, JsonSerializer.Deserialize<long?>("\"123\"", Opts()));

    [Fact]
    public void Double_AcceptsQuotedNumber()
        => Assert.Equal(4.5d, JsonSerializer.Deserialize<double?>("\"4.5\"", Opts()));

    [Fact]
    public void Unparseable_DegradesToDefault_NeverThrows()
    {
        Assert.Null(JsonSerializer.Deserialize<bool?>("\"maybe\"", Opts()));
        Assert.Null(JsonSerializer.Deserialize<int?>("{\"unexpected\":\"object\"}", Opts()));
    }

    [Fact]
    public void Director_WithBareCountryFk_AndBoolAsNumber_Deserializes_LikeProduction()
    {
        // Mirrors the failing prod payloads: countryFk + affiliatesPscInformation as bare ids,
        // isChairman as a number, isCorporate as "N".
        var json = "{\"id\":1,\"surname\":\"BELLO\",\"firstname\":\"AISHA\"," +
                   "\"countryFk\":566,\"isChairman\":1,\"isCorporate\":\"N\"," +
                   "\"affiliatesPscInformation\":796,\"affiliatesResidentialAddress\":\"N/A\"," +
                   "\"affiliateTypeFk\":{\"id\":3,\"name\":\"DIRECTOR\"}}";

        var dir = JsonSerializer.Deserialize<CacAdvancedDirectorData>(json, Opts());

        Assert.NotNull(dir);
        Assert.Equal(566, dir!.CountryFk!.Id);
        Assert.Equal("DIRECTOR", dir.AffiliateTypeFk!.Name);
        Assert.True(dir.IsChairman);
        Assert.False(dir.IsCorporate);
    }

    [Fact]
    public void CompanyData_MapsRenamedFields_FromRealPayloadShape()
    {
        // Field names taken from the real cac_advanced payload.
        var json = "{\"company_name\":\"JOYCE VENTURES\",\"rc_number\":\"10056\",\"company_id\":906200," +
                   "\"entity_type\":\"IT\",\"company_status\":\"ACTIVE\",\"company_address\":\"4 Agba Road\"," +
                   "\"registrationDate\":\"2007-03-30\"}";

        var d = JsonSerializer.Deserialize<CacAdvancedData>(json, Opts());

        Assert.NotNull(d);
        Assert.Equal("JOYCE VENTURES", d!.CompanyName);
        Assert.Equal("ACTIVE", d.CompanyStatus);
        Assert.Equal("IT", d.EntityType);
        Assert.Equal("4 Agba Road", d.CompanyAddress);   // was mapped to "address" before — would have been null
        Assert.Equal("2007-03-30", d.RegistrationDate);  // was mapped to "date_of_registration" before — would have been null
    }

    [Fact]
    public void CompanyData_RegistrationDate_AcceptsLegacySnakeCaseKey()
    {
        var d = JsonSerializer.Deserialize<CacAdvancedData>("{\"date_of_registration\":\"2007-03-30\"}", Opts());
        Assert.Equal("2007-03-30", d!.RegistrationDateLegacy);
    }

    // The real (sparse) prod payload: countryFk is the bare string "NIGERIA", every bool/object field
    // is "", affiliateTypeFk is "", and the date is under date_of_registration. Must not crash, must
    // keep both active directors, and must capture company_type + registration date.
    [Fact]
    public void RealSparsePayload_NoCrash_KeepsBothDirectors_AndCompanyType()
    {
        var response = JsonSerializer.Deserialize<SmartComplyResponse<CacAdvancedData>>(AdabPayload, Opts());
        Assert.NotNull(response?.Data);
        var d = response!.Data!;

        Assert.Equal("ADAB AGRI-RESOURCE COMPANY LTD", d.CompanyName);
        Assert.Equal("ACTIVE", d.CompanyStatus);
        Assert.Equal("RC", d.CompanyTypeRaw);
        Assert.Equal(2, d.Directors!.Count);
        Assert.Equal("NIGERIA", d.Directors[0].CountryFk!.Name);   // bare string handled

        var result = SmartComplyProvider.MapCacAdvancedToResult(d);
        Assert.Equal(2, result.Directors!.Count);                   // filter no longer drops them
        Assert.Contains(result.Directors, x => x.FullName == "SALIU");
        Assert.Contains(result.Directors, x => x.FullName == "ADNAN");
        Assert.Equal("RC", result.CompanyType);                     // coalesced from company_type
        Assert.NotNull(result.RegistrationDate);                    // from date_of_registration
    }

    [Fact]
    public void NewPayloadShape_AffiliateType_NotAffiliateTypeFk_FilteredToDirectors()
    {
        // New payload variant: affiliate type under "affiliateType" (not "affiliateTypeFk"),
        // country under "affiliateCountry" (not "countryFk"), address under "address" (not "company_address").
        var json = """
        {"status":"success","data":{
          "company_name":"BABI GLOBAL RESOURCE LTD","rc_number":"1293953","number":"1293953",
          "company_status":"","entity_type":"","company_type":"limited_liability_partnership",
          "company_address":"","address":"NO. 2 MAAHAD STREET, COMMERCIAL AREA",
          "residence_address":"NO. 2 MAAHAD STREET, COMMERCIAL AREA",
          "email_address":"","date_of_registration":"2015-10-20T10:01:33.190+00:00",
          "state":"GOMBE","lga":"",
          "directors":[
            {"surname":"MOHAMMED","firstname":"ALIYU","othername":"BABANGIDA","status":"ACTIVE",
             "gender":"Male","phoneNumber":"07038472900","nationality":"Nigerian",
             "address":"NO. 3 ABDULLAHI JALO STREET","city":"GOMBE","state":"GOMBE",
             "affiliateType":{"name":"DIRECTOR"},"affiliateCountry":{"name":"NIGERIA"},
             "dateOfBirth":"1968-12-31T23:00:00.000+00:00","identityNumber":"A04716619",
             "isChairman":false,"isCorporate":false,"isDesignated":false,
             "numSharesAlloted":0,"shareCapital":0,
             "affiliatesPscInformation":{},"affiliatesResidentialAddress":{}},
            {"surname":"ALIYU","firstname":"BABANGIDA","othername":"MOHAMMED","status":"ACTIVE",
             "gender":"Male","nationality":"Nigerian",
             "address":"NO. 3 ABDULLAHI JALO STREET","city":"GOMBE","state":"GOMBE",
             "affiliateType":{"name":"SHAREHOLDER"},"affiliateCountry":{"name":"NIGERIA"},
             "numSharesAlloted":800000,"typeOfShares":"Ordinary",
             "isChairman":false,"isCorporate":false,"isDesignated":false,"shareCapital":0,
             "affiliatesPscInformation":{},"affiliatesResidentialAddress":{}}
          ]
        },"message":"Business Registration details retrieved successfully"}
        """;

        var response = JsonSerializer.Deserialize<SmartComplyResponse<CacAdvancedData>>(json, Opts());
        Assert.NotNull(response?.Data);
        var d = response!.Data!;

        // Address falls back from company_address (empty) → address field
        var result = SmartComplyProvider.MapCacAdvancedToResult(d);
        Assert.Equal("NO. 2 MAAHAD STREET, COMMERCIAL AREA", result.Address);

        // Both DIRECTOR and SHAREHOLDER rows are kept; SECRETARY/PSC/PRESENTER are dropped
        Assert.Equal(2, result.Directors!.Count);
        Assert.Contains(result.Directors, d => d.AffiliateType == "DIRECTOR");
        Assert.Contains(result.Directors, d => d.AffiliateType == "SHAREHOLDER");
        Assert.All(result.Directors, d => Assert.Equal("NIGERIA", d.Country));
    }

    private const string AdabPayload = """
{"status":"success","data":{"lga":"","valid":true,"directors":[{"id":502892,"lga":"","city":"","email":"","state":"","gender":"MALE","status":"ACTIVE","address":"","surname":"","formType":"","postcode":"","rcNumber":"1760686","countryFk":"NIGERIA","firstname":"","otherName":"SALIU","formerName":"","isChairman":"","occupation":"","dateOfBirth":"","isCorporate":"","nationality":"NIGERIA","phoneNumber":"","isDesignated":"","isPublicUser":"","streetNumber":"","typeOfShares":"","formerSurname":"","formerNameType":"","identityNumber":"","affiliateTypeFk":"","corporationName":"","formerFirstName":"","formerOtherName":"","numSharesAlloted":"","dateOfAppointment":"2023-04-10T23:00:00.000+00:00","formerNationality":"","corporationCompany":"","accreditationnumber":"","affiliatesPscInformation":"","otherDirectorshipDetails":"","affiliatesResidentialAddress":""},{"id":1235193,"lga":"","city":"","email":"","state":"","gender":"MALE","status":"ACTIVE","address":"","surname":"","formType":"","postcode":"","rcNumber":"1760686","countryFk":"NIGERIA","firstname":"","otherName":"ADNAN","formerName":"","isChairman":"","occupation":"","dateOfBirth":"","isCorporate":"","nationality":"NIGERIA","phoneNumber":"","isDesignated":"","isPublicUser":"","streetNumber":"","typeOfShares":"","formerSurname":"","formerNameType":"","identityNumber":"","affiliateTypeFk":"","corporationName":"","formerFirstName":"","formerOtherName":"","numSharesAlloted":"","dateOfAppointment":"2023-04-10T23:00:00.000+00:00","formerNationality":"","corporationCompany":"","accreditationnumber":"","affiliatesPscInformation":"","otherDirectorshipDetails":"","affiliatesResidentialAddress":""}],"firstName":"ADAB AGRI-RESOURCE COMPANY LTD","firstname":"ADAB AGRI-RESOURCE COMPANY LTD","id_number":"1760686","rc_number":"1760686","first_name":"ADAB AGRI-RESOURCE COMPANY LTD","entity_type":"","status_more":"","company_name":"ADAB AGRI-RESOURCE COMPANY LTD","company_type":"RC","branchAddress":"","email_address":"","company_status":"ACTIVE","line_of_business":[],"date_of_registration":"2021-02-22T05:57:50.48Z"},"message":"Business Registration details retrieved successfully"}
""";
}
