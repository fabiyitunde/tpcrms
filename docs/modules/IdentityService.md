# IdentityService Module

**Module ID:** 2  
**Status:** 🟢 Completed (Core Implementation)  
**Priority:** P1  
**Bounded Context:** Identity  
**Last Updated:** 2026-02-16

---

## 1. Purpose

Handle authentication, authorization, user management, and role-based access control (RBAC) for both internal staff (Intranet) and external customers (Portal).

---

## 2. Implementation Summary

### Domain Layer (CRMS.Domain)

**Entities:** `Entities/Identity/`
- `ApplicationUser` - User with authentication fields, lockout, refresh tokens
- `ApplicationRole` - Role with permissions
- `Permission` - Granular permission codes
- `ApplicationUserRole` - User-Role join entity
- `ApplicationRolePermission` - Role-Permission join entity

**Constants:** `Constants/`
- `Roles.cs` - Predefined role names (SystemAdmin, LoanOfficer, CreditOfficer, etc.)
- `Permissions.cs` - Permission codes organized by module

**Interfaces:** `Interfaces/`
- `IUserRepository`, `IRoleRepository`, `IPermissionRepository`

### Application Layer (CRMS.Application)

**DTOs:** `Identity/DTOs/AuthDtos.cs`
- `LoginRequest`, `LoginResponse`, `RefreshTokenRequest`
- `RegisterUserRequest`, `ChangePasswordRequest`
- `UserDto`, `UserSummaryDto`, `RoleDto`, `PermissionDto`

**Interfaces:** `Identity/Interfaces/IAuthService.cs`
- `IAuthService` - Login, logout, refresh token, change password
- `ITokenService` - JWT generation
- `IPasswordHasher` - Password hashing

**Commands:** `Identity/Commands/`
- `RegisterUserCommand` / `RegisterUserHandler`

**Queries:** `Identity/Queries/`
- `GetUserByIdQuery`, `GetAllUsersQuery`

### Infrastructure Layer (CRMS.Infrastructure)

**Identity Services:** `Identity/`
- `TokenService` - JWT token generation with roles and permissions
- `PasswordHasher` - PBKDF2 password hashing
- `AuthService` - Authentication logic, lockout handling
- `JwtSettings` - Configuration class

**EF Configurations:** `Persistence/Configurations/Identity/`
- User, Role, Permission, UserRole, RolePermission configurations

**Repositories:** `Persistence/Repositories/`
- `UserRepository`, `RoleRepository`, `PermissionRepository`

### API Layer (CRMS.API)

**Controllers:**
- `AuthController` - Login, logout, refresh, change password
- `UsersController` - User CRUD operations

---

## 3. Predefined Roles

| Role | Description | User Type |
|------|-------------|-----------|
| SystemAdmin | Full system access | Staff |
| LoanOfficer | Initiates corporate loans | Staff |
| CreditOfficer | Reviews referred applications | Staff |
| RiskManager | Override authority | Staff |
| BranchApprover | Branch-level approval | Staff |
| HOReviewer | Head office review | Staff |
| CommitteeMember | Committee voting | Staff |
| FinalApprover | Final approval authority | Staff |
| Operations | Disbursement operations | Staff |
| Auditor | Read-only audit access | Staff |
| Customer | Self-service applicant | Customer |

---

## 4. Security Features

- JWT authentication with configurable expiry
- Refresh token rotation
- PBKDF2 password hashing (100,000 iterations)
- Account lockout after 5 failed attempts (30 min)
- Role-based authorization
- Permission-based claims in JWT

---

## 5. API Endpoints

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | /api/v1/auth/login | User login | No |
| POST | /api/v1/auth/refresh | Refresh access token | No |
| POST | /api/v1/auth/logout | Logout (revoke token) | Yes |
| POST | /api/v1/auth/change-password | Change password | Yes |
| GET | /api/v1/users | List all users | Yes |
| GET | /api/v1/users/{id} | Get user by ID | Yes |
| POST | /api/v1/users | Register new user | Yes |

---

## 6. Configuration

Configure via appsettings.json or environment variables:

| Setting | Description | Default |
|---------|-------------|---------|
| JwtSettings:Secret | JWT signing key (min 32 chars) | - |
| JwtSettings:Issuer | Token issuer | CRMS.API |
| JwtSettings:Audience | Token audience | CRMS.Clients |
| JwtSettings:AccessTokenExpiryMinutes | Access token lifetime | 60 |
| JwtSettings:RefreshTokenExpiryDays | Refresh token lifetime | 7 |

---

## 7. Database Tables

- `Users` - User accounts
- `Roles` - Role definitions
- `Permissions` - Permission definitions
- `UserRoles` - User-Role assignments
- `RolePermissions` - Role-Permission assignments

---

## 8. Pending Enhancements

- [ ] Add data seeding for roles and permissions
- [ ] Add user update/delete endpoints
- [ ] Add role management endpoints
- [ ] Add password reset (forgot password) flow
- [ ] Add email verification for customer registration

---

## Document History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-16 | Initial implementation |
