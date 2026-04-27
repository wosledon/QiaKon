namespace QiaKon.Contracts;

public interface IEntity
{
    public Guid Id { get; set; }
}

public interface IAuditable
{
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

public interface ISoftDelete
{
    public bool IsDeleted { get; set; }
}

public interface IVersionable
{
    public byte[] RowVersion { get; set; }
}

public abstract class EntityBase : IEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

public abstract class AuditableEntityBase : EntityBase, IAuditable
{
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

public abstract class SoftDeleteEntityBase : AuditableEntityBase, ISoftDelete
{
    public bool IsDeleted { get; set; }
}