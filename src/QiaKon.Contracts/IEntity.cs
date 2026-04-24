namespace QiaKon.Contracts;

public interface IEntity
{
    public Guid Id { get; set; }
}

public abstract class EntityBase : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

public interface ISoftDelete
{
    public bool IsDeleted { get; set; }
}

public abstract class SoftDeleteEntityBase : EntityBase, ISoftDelete
{
    public bool IsDeleted { get; set; }
}