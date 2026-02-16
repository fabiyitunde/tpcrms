using CRMS.Application.Common;
using CRMS.Application.Identity.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Identity.Queries;

public record GetUserByIdQuery(Guid Id) : IRequest<ApplicationResult<UserDto>>;

public class GetUserByIdHandler : IRequestHandler<GetUserByIdQuery, ApplicationResult<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;

    public GetUserByIdHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
    }

    public async Task<ApplicationResult<UserDto>> Handle(GetUserByIdQuery request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(request.Id, ct);
        if (user == null)
            return ApplicationResult<UserDto>.Failure("User not found");

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

public record GetAllUsersQuery : IRequest<ApplicationResult<List<UserSummaryDto>>>;

public class GetAllUsersHandler : IRequestHandler<GetAllUsersQuery, ApplicationResult<List<UserSummaryDto>>>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;

    public GetAllUsersHandler(IUserRepository userRepository, IRoleRepository roleRepository)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
    }

    public async Task<ApplicationResult<List<UserSummaryDto>>> Handle(GetAllUsersQuery request, CancellationToken ct = default)
    {
        var users = await _userRepository.GetAllAsync(ct);
        var result = new List<UserSummaryDto>();

        foreach (var user in users)
        {
            var roles = await _roleRepository.GetUserRolesAsync(user.Id, ct);
            result.Add(new UserSummaryDto(
                user.Id,
                user.Email,
                user.FullName,
                user.Type.ToString(),
                user.Status.ToString(),
                roles.Select(r => r.Name).ToList()
            ));
        }

        return ApplicationResult<List<UserSummaryDto>>.Success(result);
    }
}
