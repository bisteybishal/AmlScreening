using AmlScreening.Application.Interface.Provider;
using AmlScreening.Domain.Entities;
using System.Xml.Linq;

namespace AmlScreening.Infrastructure.Provider
{
    public class EuAmlSourceStrategy : IAmlSourceStrategy
    {
        private readonly HttpClient _httpClient;
        public  string SourceCode => "EU";

        public EuAmlSourceStrategy(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("EuAmlClient");
        }

        public async Task<List<AmlRecord>> GetRecordsAsync()
        {
            //const string apiUrl = "https://webgate.ec.europa.eu/fsd/fsf/public/files/" + "xmlFullSanctionsList_1_1/content?token=dG9rZW4tMjAxNw";
            //var xml = await _httpClient.GetStringAsync(apiUrl);
            const string apiUrl = "fsd/fsf/public/files/xmlFullSanctionsList_1_1/content?token=dG9rZW4tMjAxNw";
            var xml = await _httpClient.GetStringAsync(apiUrl);
            var document = XDocument.Parse(xml);
            var records = new List<AmlRecord>();
            AddEuRecords(document, records);
            return records;
        }
        private static void AddEuRecords(XDocument document, List<AmlRecord> records)
        {
            var entities = document.Descendants().Where(d => d.Name.LocalName == "sanctionEntity");
            foreach (var entity in entities)
            {
                var refernceNo = entity.Attribute("euReferenceNumber")?.Value;
                var subjectType = entity.Elements().FirstOrDefault(x => x.Name.LocalName == "subjectType")?.Attribute("code")?.Value;
                var recordType = subjectType == "person" ? "Individual" : "Entity";
                var nameallias = entity.Elements().FirstOrDefault(x => x.Name.LocalName == "nameAlias");
                if (nameallias == null)
                {
                    continue;
                }
                var fullName = nameallias.Attribute("wholeName")?.Value;
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    var firstName =
                        nameallias.Attribute("firstName")
                            ?.Value;

                    var middleName =
                        nameallias.Attribute("middleName")
                            ?.Value;

                    var lastName =
                        nameallias.Attribute("lastName")
                            ?.Value;

                    fullName = string.Join(
                        " ",
                        new[]
                        {
                            firstName,
                            middleName,
                            lastName
                        }
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(x)));
                }

                if (string.IsNullOrWhiteSpace(fullName) ||
                    string.IsNullOrWhiteSpace(refernceNo))
                {
                    continue;
                }

                var dobText = entity
                    .Elements()
                    .FirstOrDefault(x =>
                        x.Name.LocalName == "birthdate")
                    ?.Attribute("birthdate")
                    ?.Value;

                DateTime? dob = null;

                if (DateTime.TryParse(
                    dobText,
                    out var parsedDob))
                {
                    dob = parsedDob;
                }

                var nationality = entity
                    .Elements()
                    .FirstOrDefault(x =>
                        x.Name.LocalName == "citizenship")
                    ?.Attribute("countryDescription")
                    ?.Value;

                var remarks = entity
                    .Elements()
                    .FirstOrDefault(x =>
                        x.Name.LocalName == "remark")
                    ?.Value;

                records.Add(new AmlRecord
                {
                    RecordType = recordType,

                    FullName = fullName.Trim(),

                    DateOfBirth = dob,

                    Nationality =
                        CleanValue(nationality),

                    ReferenceNo =
                        refernceNo.Trim(),

                    SourceName =
                        "EU Sanctions",

                    Remarks =
                        CleanValue(remarks),

                    IsActive = true
                });
            }
        }

        private static string CleanValue(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim();
        }

    }
}


