using CRMS.Domain.Common;

namespace CRMS.Domain.Entities.Identity;

public class Permission : Entity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Module { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    private Permission() { }

    public static Permission Create(string code, string name, string module, string? description = null)
    {
        return new Permission
        {
            Code = code,
            Name = name,
            Module = module,
            Description = description
        };
    }
}
