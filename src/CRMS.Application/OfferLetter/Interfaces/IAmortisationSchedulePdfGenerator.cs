namespace CRMS.Application.OfferLetter.Interfaces;

public interface IAmortisationSchedulePdfGenerator
{
    Task<byte[]> GenerateAsync(OfferLetterData data, CancellationToken ct = default);
}
