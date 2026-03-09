using CRMS.Application.Common;
using CRMS.Application.Identity.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Identity.Commands;

public record UpdateUserCommand(
    Guid UserId,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    List<string>? Roles
) : IRequest<ApplicationResult<UserDto>>;

public class UpdateUserHandler : IRequestHandler<UpdateUserCommand, ApplicationResult<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<UserDto>> Handle(UpdateUserCommand request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, ct);
        if (user == null)
            return ApplicationResult<UserDto>.Failure("User not found");

        var updateResult = user.UpdateProfile(request.FirstName, request.LastName, request.PhoneNumber);
        if (updateResult.IsFailure)
            return ApplicationResult<UserDto>.Failure(updateResult.Error);

        if (request.Roles != null)
        {
            user.ClearRoles();
            foreach (var roleName in request.Roles)
            {
                var role = await _roleRepository.GetByNameAsync(roleName, ct);
                if (role != null)
                    user.AddRole(role);
            }
        }

        await _userRepository.UpdateAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var roles = await _roleRepository.GetUserRolesAsync(user.Id, ct);
        var permissions = await _permissionRepository.GetUserPermissionsAsync(user.Id, ct);

        return ApplicationResult<UserDto>.Success(new UserDto(
            user.Id, user.Email, user.UserName, user.FirstName, user.LastName, user.FullName,
            user.Type.ToString(), user.Status.ToString(), user.PhoneNumber, user.BranchId,
            user.LastLoginAt, roles.Select(r => r.Name).ToList(), permissions.Select(p => p.Code).ToList()
        ));
    }
}
