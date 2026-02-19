# Feedback Issue FB-003: Role Names Mismatch Between SeedData and Roles.cs

**Original Issue:** NEW — introduced by Phase E seed data fix
**Verified Status:** NEW DEFECT 🔴
**Severity:** HIGH — Authorization will fail at startup for multiple workflows

---

## Summary

`SeedData.cs` (which populates the database on first run) seeds different role names than those defined in `Roles.cs` (used by `[Authorize]` attributes across controllers). This creates a broken authorization model where some API endpoints restrict access to roles that do not exist in the database, and some database roles are never referenced in code.

---

## Mismatch Table

| Role in `SeedData.cs` | Role in `Roles.cs` | Impact |
|-----------------------|--------------------|--------|
| `SystemAdmin` | `SystemAdmin` | ✅ Match |
| `BranchManager` | `BranchApprover` | ❌ Mismatch — controllers using `BranchApprover` will block all users |
| `CreditOfficer` | `CreditOfficer` | ✅ Match |
| `CreditAnalyst` | *(not in Roles.cs)* | ⚠️ Seeded but never used in authorization |
| `HOReviewer` | `HOReviewer` | ✅ Match |
| `RiskManager` | `RiskManager` | ✅ Match |
| `CommitteeMember` | `CommitteeMember` | ✅ Match |
| `CommitteeChair` | *(not in Roles.cs)* | ⚠️ Seeded but never used in authorization |
| `Auditor` | `Auditor` | ✅ Match |
| `CustomerService` | `Customer` | ❌ Mismatch — customer-facing features will block all users |
| *(not seeded)* | `LoanOfficer` | ❌ Missing — loan officers cannot log in with this role |
| *(not seeded)* | `FinalApprover` | ❌ Missing — final approval step has no authorized users |
| *(not seeded)* | `Operations` | ❌ Missing — disbursement operations have no authorized users |

---

## Affected Flows

Based on the role mapping gaps:

1. **Branch approval workflow** — `BranchApprover` referenced in workflow engine and potentially controllers, but no user can hold this role (database has `BranchManager` instead)
2. **Final disbursement** — `FinalApprover` and `Operations` roles have no DB entries; no user can be assigned to these roles
3. **Loan origination** — `LoanOfficer` role is not seeded; loan officers cannot operate unless a manual DB insert is done
4. **Customer portal (Phase 2)** — `Customer` role is not seeded; `CustomerService` is seeded but unreferenced

---

## Required Fix

**Option A (Recommended): Update `SeedData.cs` to match `Roles.cs` exactly**

Replace the role names in `SeedData.cs` with the constants from `Roles.cs`:

```csharp
// Use Roles.cs constants to prevent future drift
var roles = new[]
{
    ApplicationRole.Create(Roles.SystemAdmin, "Full system access", RoleType.System),
    ApplicationRole.Create(Roles.LoanOfficer, "Initiates and manages corporate loan applications", RoleType.System),
    ApplicationRole.Create(Roles.CreditOfficer, "Reviews referred applications and makes credit decisions", RoleType.System),
    ApplicationRole.Create(Roles.RiskManager, "Senior staff with override authority and risk analysis", RoleType.System),
    ApplicationRole.Create(Roles.BranchApprover, "Branch-level approval authority for corporate loans", RoleType.System),
    ApplicationRole.Create(Roles.HOReviewer, "Head office review for corporate loans", RoleType.System),
    ApplicationRole.Create(Roles.CommitteeMember, "Committee voting and comments for corporate loans", RoleType.System),
    ApplicationRole.Create(Roles.FinalApprover, "Final loan approval authority", RoleType.System),
    ApplicationRole.Create(Roles.Operations, "Disbursement and booking operations", RoleType.System),
    ApplicationRole.Create(Roles.Auditor, "Read-only audit access", RoleType.System),
    ApplicationRole.Create(Roles.Customer, "Self-service retail loan applicant", RoleType.System)
};
```

**Option B: Update `Roles.cs` to match `SeedData.cs`**

If the team deliberately chose the `SeedData.cs` names (e.g., `BranchManager` over `BranchApprover`), then update `Roles.cs` constants and all `[Authorize(Roles = ...)]` attributes to use the new names. This is more work but may reflect the bank's actual job title naming.

**Recommended: Option A** — `Roles.cs` appears to be the authoritative source referenced in documentation and authorization attributes. `SeedData.cs` should align to it.

---

## Prevention

To prevent future drift, `SeedData.cs` should reference `Roles.cs` constants directly (as shown in the Option A example above) rather than using string literals. A mismatch between these two files will only be caught at runtime (when a user logs in and authorization fails), not at compile time.

---

## Test Scenarios

After fixing:
- [ ] System boots successfully and all roles appear in the `Roles` table
- [ ] A user assigned `BranchApprover` role can access branch approval endpoints
- [ ] A user assigned `LoanOfficer` can initiate a loan application
- [ ] A user assigned `FinalApprover` can access final approval endpoints
- [ ] A user assigned `Operations` can access disbursement endpoints
- [ ] `Roles.AllRoles` and the seeded role names are in 1:1 correspondence
