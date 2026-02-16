using CRMS.Application.Common;
using CRMS.Application.Identity.Commands;
using CRMS.Application.Identity.DTOs;
using CRMS.Application.Identity.Queries;
using CRMS.Domain.Entities.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRMS.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IRequestHandler<RegisterUserCommand, ApplicationResult<UserDto>> _registerHandler;
    private readonly IRequestHandler<GetUserByIdQuery, ApplicationResult<UserDto>> _getByIdHandler;
    private readonly IRequestHandler<GetAllUsersQuery, ApplicationResult<List<UserSummaryDto>>> _getAllHandler;

    public UsersController(
        IRequestHandler<RegisterUserCommand, ApplicationResult<UserDto>> registerHandler,
        IRequestHandler<GetUserByIdQuery, ApplicationResult<UserDto>> getByIdHandler,
        IRequestHandler<GetAllUsersQuery, ApplicationResult<List<UserSummaryDto>>> getAllHandler)
    {
        _registerHandler = registerHandler;
        _getByIdHandler = getByIdHandler;
        _getAllHandler = getAllHandler;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _getAllHandler.Handle(new GetAllUsersQuery(), ct);
        return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getByIdHandler.Handle(new GetUserByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterUserApiRequest request, CancellationToken ct)
    {
        var userType = Enum.Parse<UserType>(request.UserType, true);
        
        var command = new RegisterUserCommand(
            request.Email,
            request.UserName,
            request.Password,
            request.FirstName,
            request.LastName,
            userType,
            request.PhoneNumber,
            request.BranchId,
            request.Roles
        );

        var result = await _registerHandler.Handle(command, ct);
        return result.IsSuccess 
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data) 
            : BadRequest(result.Error);
    }
}

public record RegisterUserApiRequest(
    string Email,
    string UserName,
    string Password,
    string FirstName,
    string LastName,
    string UserType,
    string? PhoneNumber,
    Guid? BranchId,
    List<string>? Roles
);
