using CRMS.Application.Common;
using CRMS.Application.Identity.DTOs;
using CRMS.Application.Identity.Interfaces;
using CRMS.Domain.Entities.Identity;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Identity.Commands;

public record RegisterUserCommand(
    string Email,
    string UserName,
    string Password,
    string FirstName,
    string LastName,
    UserType Type,
    string? PhoneNumber,
    Guid? BranchId,
    List<string>? Roles
) : IRequest<ApplicationResult<UserDto>>;

public class RegisterUserHandler : IRequestHandler<RegisterUserCommand, ApplicationResult<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterUserHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<UserDto>> Handle(RegisterUserCommand request, CancellationToken ct = default)
    {
        if (await _userRepository.ExistsAsync(request.Email, ct))
            return ApplicationResult<UserDto>.Failure($"User with email '{request.Email}' already exists");

        var userResult = ApplicationUser.Create(
            request.Email,
            request.UserName,
            request.FirstName,
            request.LastName,
            request.Type,
            request.PhoneNumber,
            request.BranchId
        );

        if (userResult.IsFailure)
            return ApplicationResult<UserDto>.Failure(userResult.Error);

        var user = userResult.Value;
        user.SetPasswordHash(_passwordHasher.HashPassword(request.Password));

        if (request.Roles?.Any() == true)
        {
            foreach (var roleName in request.Roles)
            {
                var role = await _roleRepository.GetByNameAsync(roleName, ct);
                if (role != null)
                    user.AddRole(role);
            }
        }

        await _userRepository.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var roles = await _roleRepository.GetUserRolesAsync(user.Id, ct);
        var permissions = await _permissionRepository.GetUserPermissionsAsync(user.Id, ct);

        return ApplicationResult<UserDto>.Success(new UserDto(
            user.Id,
            user.Email,
            user.UserName,
            user.FirstName,
            user.LastName,
            user.FullName,
            user.Type.ToString(),
            user.Status.ToString(),
            user.PhoneNumber,
            user.BranchId,
            user.LastLoginAt,
            roles.Select(r => r.Name).ToList(),
            permissions.Select(p => p.Code).ToList()
        ));
    }
}
