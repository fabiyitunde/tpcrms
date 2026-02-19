# Audit Report: IdentityService Module

**Module ID:** 2
**Audit Date:** 2026-02-17
**Auditor:** Domain Expert Review
**Module Status (Documented):** 🟢 Completed (Core Implementation)
**Audit Verdict:** ⚠️ Critical Security Gaps Present

---

## 1. Executive Summary

The IdentityService module provides a solid foundation with PBKDF2 password hashing, JWT token management, refresh token rotation, and account lockout. However, several critical security and operational gaps exist: no role/permission seeding, missing password reset flow, no email verification, and critical questions around refresh token storage and revocation that must be addressed before production deployment.

---

## 2. Critical Security Issues

### 2.1 Refresh Token Storage Not Specified (HIGH RISK)

The module documents refresh token rotation but does not specify whether refresh tokens are stored in plain text or hashed in the database. If stored in plain text, a database breach would expose all active user sessions.

**Requirement:** Refresh tokens must be stored as a SHA256 hash (or similar). Only the hash is stored; the raw token is returned to the client once and never stored in plain text.

### 2.2 Token Revocation on Logout Unclear

The `POST /api/v1/auth/logout` endpoint is documented as "revoke token." It is not clear whether this invalidates the JWT access token (JWTs are stateless by nature) or only revokes the refresh token. A user logging out should not be able to use a previously issued (still-valid) access token.

**Recommendation:** Implement a short access token expiry (e.g., 15 minutes) and maintain a server-side refresh token revocation list. Logout should invalidate the refresh token in the database. Document this clearly.

### 2.3 JWT Secret Validation Not Enforced in Code

The documentation states the JWT signing key must be a minimum of 32 characters, but there is no startup validation enforcing this. A misconfigured short key (e.g., in development) would create weak tokens.

**Recommendation:** Add startup validation:
```csharp
if (jwtSettings.Secret.Length < 32)
    throw new InvalidOperationException("JWT Secret must be at least 32 characters");
```

### 2.4 No Rate Limiting on Auth Endpoints Beyond Lockout

Account lockout after 5 failed attempts is implemented, but there is no IP-based rate limiting. An attacker can create multiple accounts or target multiple usernames from the same IP without restriction.

**Recommendation:** Apply rate limiting middleware (e.g., `AspNetCoreRateLimit`) on `/api/v1/auth/login` and `/api/v1/auth/refresh`.

### 2.5 `BranchId` Not Included in JWT Claims

The `BranchApprover` role requires filtering loan applications by branch. If `BranchId` is not included in the JWT claims, the API cannot enforce branch-level data scoping without an additional database lookup per request.

**Recommendation:** Include `BranchId` as a custom claim in the JWT token for staff users with branch-scoped roles.

---

## 3. Missing Features (Documented as Pending)

### 3.1 No Role and Permission Seeding

No seed data exists for roles and permissions. After a fresh deployment, no users can be created (chicken-and-egg problem: you need a SystemAdmin to create users, but no SystemAdmin exists).

**Recommendation:** Implement a bootstrap mechanism:
- Seed all predefined roles (from `Roles.cs`) and permissions (from `Permissions.cs`) on first run
- Create a default SystemAdmin account from configuration (environment variables) with a forced password change on first login

### 3.2 Password Reset (Forgot Password) Flow Missing

The `POST /api/v1/auth/forgot-password` and `POST /api/v1/auth/reset-password` flows are documented in the IntranetUI requirements but not implemented in the backend. This is a critical operational gap — locked-out users or new users cannot self-recover.

**Recommendation:** Implement time-limited password reset tokens (stored hashed in DB, e.g., 1-hour expiry) with email delivery.

### 3.3 Email Verification for Customer Registration

For Phase 2 Customer Portal, customer registration without email verification poses identity fraud risk. This should be designed now even if implemented in Phase 2.

### 3.4 User Update / Delete Endpoints Missing

Admin users cannot update user details (name, email, role assignments) or deactivate users via the API. Direct database manipulation would be required.

### 3.5 Role Management Endpoints Missing

No API endpoints exist to assign roles to users, view role memberships, or manage permissions. All role assignments would require direct database access.

---

## 4. Potential Bugs

### 4.1 `ComplianceOfficer` Role Missing

The `AuditService` and `ReportingService` reference `ComplianceOfficer` as a required role for accessing audit logs. However, the `Roles.cs` constants file does not appear to define `ComplianceOfficer` as a predefined role — it lists `Auditor` instead. This naming mismatch will cause authorization failures at runtime.

**Recommendation:** Reconcile role names across `Roles.cs`, `Permissions.cs`, and all `[Authorize(Roles = "...")]` attributes. Use a single source of truth.

### 4.2 `HOReviewer` vs `HOReview` Role Name Inconsistency

The IntranetUI requirements matrix shows "HOReviewer" as a role, and the `Roles.cs` file uses "HOReviewer", but the WorkflowEngine assigns `AssignedRole = "CreditOfficer"` to the HO Review stage. This mismatch means the workflow queue for HO Review will not correctly route to HOReviewers.

### 4.3 Lockout Duration Hardcoded

The 30-minute account lockout duration appears hardcoded. In production, this should be configurable.

---

## 5. Compliance Gaps

### 5.1 No Audit Logging for Authentication Events

The `AuditService` module defines `Login`, `Logout`, `LoginFailed`, and `PasswordChange` audit actions, but it is unclear whether `AuthService.LoginAsync()` actually calls `AuditService.LogLoginAsync()`. This must be verified — NDPA compliance requires a complete access log.

### 5.2 Session Management for Concurrent Logins

There is no documented policy for concurrent session management. A single user logging in from multiple devices simultaneously may be acceptable for some roles but not for high-privilege roles like `FinalApprover` or `SystemAdmin`.

---

## 6. Testing Gaps

- No unit tests for `TokenService` (JWT generation, claims verification)
- No unit tests for `AuthService` (login flow, lockout logic)
- No unit tests for `PasswordHasher`
- No integration tests for token refresh and revocation flow

---

## 7. Recommendations Summary

| Priority | Item |
|----------|------|
| CRITICAL | Confirm refresh tokens are stored hashed, not in plain text |
| CRITICAL | Clarify and document token revocation mechanism on logout |
| HIGH | Implement role and permission seeding for fresh deployments |
| HIGH | Enforce JWT secret minimum length at startup |
| HIGH | Implement password reset flow |
| HIGH | Reconcile `ComplianceOfficer` vs `Auditor` role naming |
| HIGH | Reconcile `HOReviewer` role with WorkflowEngine stage assignment |
| MEDIUM | Add `BranchId` claim to JWT for branch-scoped roles |
| MEDIUM | Add IP-based rate limiting on authentication endpoints |
| MEDIUM | Implement user update/delete and role management endpoints |
| LOW | Make lockout duration configurable |
| LOW | Add comprehensive unit and integration tests |
