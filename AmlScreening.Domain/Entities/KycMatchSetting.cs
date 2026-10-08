namespace AmlScreening.Domain.Entities;

public class KycMatchSetting
{
    public int Id { get; set; }


    public decimal NameWeight { get; set; }

    public decimal DateOfBirthWeight { get; set; }

    public decimal CountryWeight { get; set; }

    public decimal GenderWeight { get; set; }


    public decimal ReviewThreshold { get; set; }

    public decimal StrongMatchThreshold { get; set; }


    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }
}