using CRMS.Application.LoanPack.DTOs;

namespace CRMS.Application.LoanPack.Interfaces;

/// <summary>
/// Interface for PDF loan pack generation.
/// </summary>
public interface ILoanPackGenerator
{
    /// <summary>
    /// Generates a PDF loan pack from the provided data.
    /// </summary>
    /// <param name="data">All data required to generate the loan pack</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>PDF document as byte array</returns>
    Task<byte[]> GenerateAsync(LoanPackData data, CancellationToken ct = default);
}
