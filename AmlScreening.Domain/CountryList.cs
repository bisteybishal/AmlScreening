public class CountryList
{
    public int Id { get; set; }

    public string CountryName { get; set; }
        = string.Empty;

    public string Iso2 { get; set; }
        = string.Empty;

    public string Iso3 { get; set; }
        = string.Empty;

    public string? Nationality { get; set; }

    public bool IsActive { get; set; }


    public DateTime CreatedDate { get; set; }
}