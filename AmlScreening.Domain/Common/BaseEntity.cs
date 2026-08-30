namespace AmlScreening.Domain.Common;

public abstract class BaseEntity
{
    public int Id { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public string? UpdatedBy { get; set; }

    public string DeleteFg { get; set; } = "N";

    public DateTime? DeleteDate { get; set; }

    public string? DeleteBy { get; set; }
}
