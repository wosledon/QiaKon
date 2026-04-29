using QiaKon.Contracts;

namespace QiaKon.Shared;

internal static class QiaKonSeedData
{
    public static readonly Guid HeadquartersDepartmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid EngineeringDepartmentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid SalesDepartmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid HrDepartmentId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid FrontendDepartmentId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid BackendDepartmentId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    public static readonly Guid AdminUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid KnowledgeAdminUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid DepartmentManagerUserId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid EngineerUserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    public static readonly Guid SalesUserId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private static readonly IReadOnlyDictionary<Guid, string> DepartmentNames = new Dictionary<Guid, string>
    {
        [HeadquartersDepartmentId] = "公司总部",
        [EngineeringDepartmentId] = "研发部",
        [SalesDepartmentId] = "销售部",
        [HrDepartmentId] = "人力资源部",
        [FrontendDepartmentId] = "前端组",
        [BackendDepartmentId] = "后端组",
    };

    public static string GetDepartmentName(Guid departmentId)
        => DepartmentNames.TryGetValue(departmentId, out var name) ? name : "未知部门";

    public static IReadOnlyList<DepartmentSeed> GetDepartments()
        =>
        [
            new(HeadquartersDepartmentId, "公司总部", null, DateTime.UtcNow.AddYears(-2)),
            new(EngineeringDepartmentId, "研发部", HeadquartersDepartmentId, DateTime.UtcNow.AddYears(-1)),
            new(SalesDepartmentId, "销售部", HeadquartersDepartmentId, DateTime.UtcNow.AddMonths(-6)),
            new(HrDepartmentId, "人力资源部", HeadquartersDepartmentId, DateTime.UtcNow.AddMonths(-3)),
            new(FrontendDepartmentId, "前端组", EngineeringDepartmentId, DateTime.UtcNow.AddMonths(-2)),
            new(BackendDepartmentId, "后端组", EngineeringDepartmentId, DateTime.UtcNow.AddMonths(-2)),
        ];

    public static Guid GetDefaultDepartmentId() => EngineeringDepartmentId;

    public static AccessLevel ResolveAccessLevel(AccessLevel? accessLevel, string? visibility)
    {
        if (accessLevel.HasValue)
        {
            return accessLevel.Value;
        }

        return visibility?.ToLowerInvariant() switch
        {
            "public" => AccessLevel.Public,
            "department" => AccessLevel.Department,
            "private" => AccessLevel.Restricted,
            _ => AccessLevel.Department,
        };
    }
}

internal sealed record DepartmentSeed(Guid Id, string Name, Guid? ParentId, DateTime CreatedAt);
