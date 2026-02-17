using System.Threading.Channels;
using CRMS.Application.Common;
using CRMS.Application.CreditBureau.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that processes credit check requests asynchronously.
/// Triggered when a loan application is approved at branch level.
/// </summary>
public class CreditCheckBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CreditCheckBackgroundService> _logger;
    private readonly Channel<CreditCheckRequest> _channel;

    public CreditCheckBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<CreditCheckBackgroundService> logger,
        Channel<CreditCheckRequest> channel)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _channel = channel;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Credit Check Background Service started");

        await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation(
                    "Processing credit checks for loan application {LoanApplicationId}",
                    request.LoanApplicationId);

                await ProcessCreditChecksAsync(request, stoppingToken);

                _logger.LogInformation(
                    "Completed credit checks for loan application {LoanApplicationId}",
                    request.LoanApplicationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error processing credit checks for loan application {LoanApplicationId}",
                    request.LoanApplicationId);
            }
        }

        _logger.LogInformation("Credit Check Background Service stopped");
    }

    private async Task ProcessCreditChecksAsync(CreditCheckRequest request, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        var handler = scope.ServiceProvider
            .GetRequiredService<IRequestHandler<ProcessLoanCreditChecksCommand, ApplicationResult<CreditCheckBatchResultDto>>>();

        var command = new ProcessLoanCreditChecksCommand(request.LoanApplicationId, request.SystemUserId);
        var result = await handler.Handle(command, ct);

        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "Credit check batch completed for {LoanApplicationId}: {Total} total, {Success} successful, {Failed} failed, {NotFound} not found",
                request.LoanApplicationId,
                result.Data!.TotalChecks,
                result.Data.Successful,
                result.Data.Failed,
                result.Data.NotFound);
        }
        else
        {
            _logger.LogWarning(
                "Credit check batch failed for {LoanApplicationId}: {Error}",
                request.LoanApplicationId,
                result.Error);
        }
    }
}

public record CreditCheckRequest(Guid LoanApplicationId, Guid SystemUserId);

public class CreditCheckQueue : Application.CreditBureau.Interfaces.ICreditCheckQueue
{
    private readonly Channel<CreditCheckRequest> _channel;
    private readonly ILogger<CreditCheckQueue> _logger;

    public CreditCheckQueue(Channel<CreditCheckRequest> channel, ILogger<CreditCheckQueue> logger)
    {
        _channel = channel;
        _logger = logger;
    }

    public async ValueTask QueueCreditCheckAsync(Guid loanApplicationId, Guid systemUserId, CancellationToken ct = default)
    {
        var request = new CreditCheckRequest(loanApplicationId, systemUserId);
        await _channel.Writer.WriteAsync(request, ct);
        _logger.LogInformation("Queued credit checks for loan application {LoanApplicationId}", loanApplicationId);
    }
}
