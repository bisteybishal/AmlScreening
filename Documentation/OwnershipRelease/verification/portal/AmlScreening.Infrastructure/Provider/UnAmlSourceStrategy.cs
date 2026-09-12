using AmlScreening.Application.Interface.Provider;
using AmlScreening.Domain.Entities;
using System.Globalization;
using System.Xml.Linq;

namespace AmlScreening.Infrastructure.Provider
{
    public sealed class UnAmlSourceStrategy : IAmlSourceStrategy
    {
        private readonly HttpClient _httpClient;
        public string SourceCode => "UN";
        public UnAmlSourceStrategy(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("UnAmlClient");
        }

        public async Task<List<AmlRecord>> GetRecordsAsync(CancellationToken cancellationToken = default)
        {
            //const string apiUrl = "https://scsanctions.un.org/resources/xml/en/name/consolidated.xml";
            //var xml = await _httpClient.GetStringAsync(apiUrl, cancellationToken);

            const string apiUrl = "resources/xml/en/name/consolidated.xml"; // relative to BaseAddress
            var xml = await _httpClient.GetStringAsync(apiUrl, cancellationToken);
            var document = XDocument.Parse(xml);
            var records = new List<AmlRecord>();
            AddIndividual(document, records);
            AddEntities(document, records);
            return records;

        }

        private static void AddIndividual(XDocument document, List<AmlRecord> records)
        {
            var individualcount = document.Descendants().Count(X => X.Name.LocalName == "INDIVIDUAL");
            var individual = document.Descendants().Where(X => X.Name.LocalName == "INDIVIDUAL");
            foreach (var item in individual)
            {
                var firstName =
                 GetValue(item, "FIRST_NAME");

                var secondName =
                    GetValue(item, "SECOND_NAME");

                var thirdName =
                    GetValue(item, "THIRD_NAME");

                var fourthName =
                    GetValue(item, "FOURTH_NAME");

                var fullname = string.Join(" ", new[]
                { firstName, secondName, thirdName, fourthName }.
                Where(x => !string.IsNullOrWhiteSpace(x) && !string.Equals(x, "n", StringComparison.OrdinalIgnoreCase)));
                var referenceNo = GetValue(item, "REFERENCE_NUMBER");
                var nationality = item
      .Descendants()
      .FirstOrDefault(x =>
          x.Name.LocalName == "NATIONALITY")
      ?.Descendants()
      .FirstOrDefault(x =>
          x.Name.LocalName == "VALUE")
      ?.Value;

                var remarks =
                    GetValue(item, "COMMENTS1");

                var dob =
                    GetFirstDateOfBirth(item);
                if (string.IsNullOrWhiteSpace(fullname) || string.IsNullOrWhiteSpace(referenceNo))
                { continue; }
                records.Add(new AmlRecord
                {
                    RecordType = "Individual",
                    FullName = fullname,
                    ReferenceNo = referenceNo,
                    Nationality = CleanValue(nationality),
                    Remarks = CleanValue(remarks),
                    DateOfBirth = dob,
                    SourceName = "UN Sanctions"
                });
            }
        }

        public static void AddEntities(XDocument document, List<AmlRecord> records)
        {
            foreach (var item in document.Descendants("ENTITY"))
            {
                var fullname = GetValue(item, "FIRST_NAME");
                var referenceNo = GetValue(item, "REFERENCE_NUMBER");
                var remarks = GetValue(item, "COMMENTS1");
                var dob = GetFirstDateOfBirth(item);
                if (string.IsNullOrWhiteSpace(fullname) || string.IsNullOrWhiteSpace(referenceNo))
                { continue; }
                records.Add(new AmlRecord
                {
                    RecordType = "Entity",
                    FullName = fullname,
                    ReferenceNo = referenceNo,
                    Remarks = CleanValue(remarks),
                    SourceName = "UN Sanctions"
                });

            }
        }
        private static string GetValue(
            XElement element,
            string name)
        {
            return element.Element(name)?.Value?.Trim()
                   ?? string.Empty;
        }
        private static string CleanValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            if (value.Equals(
                "na",
                StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return value.Trim();
        }

        private static DateTime? GetFirstDateOfBirth(
     XElement individual)
        {
            var dates =
                individual
                    .Element("INDIVIDUAL_DATE_OF_BIRTH")
                    ?.Elements("DATE");

            if (dates == null)
                return null;

            foreach (var date in dates)
            {
                if (DateTime.TryParse(
                    date.Value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var result))
                {
                    return result;
                }
            }

            return null;
        }

    }

}
