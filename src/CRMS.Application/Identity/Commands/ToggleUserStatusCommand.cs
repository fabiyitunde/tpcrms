using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Identity.Commands;

public record ToggleUserStatusCommand(Guid UserId, bool Deactivate) : IRequest<ApplicationResult>;

public class ToggleUserStatusHandler : IRequestHandler<ToggleUserStatusCommand, ApplicationResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ToggleUserStatusHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ToggleUserStatusCommand request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, ct);
        if (user == null)
            return ApplicationResult.Failure("User not found");

        var result = request.Deactivate ? user.Deactivate() : user.Activate();
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _userRepository.UpdateAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}
