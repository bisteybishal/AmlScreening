using AmlScreening.Application.Interface.Provider;
using AmlScreening.Domain.Entities;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Xml.Linq;

namespace AmlScreening.Infrastructure.Provider
{
    public class UnDataProvider : IAmlDataProvider
    {
        private readonly HttpClient _httpClient;
        public string sourcecode => "UN";
        public UnDataProvider(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<AmlRecord>> GetRecordsAsync()
        {
            const string apiUrl = "https://scsanctions.un.org/resources/xml/en/name/consolidated.xml";
            var xml = await _httpClient.GetStringAsync(apiUrl);
            var document = XDocument.Parse(xml);
            var records = new List<AmlRecord>();
            AddIndividual(document, records);
            AddEntities(document, records);
            return records;

        }

        private static void AddIndividual(XDocument document, List<AmlRecord> records)
        {
            foreach (var item in document.Descendants("INDIVIDUALS"))
            {
                var firstName =
                 GetValue(item, "FIRST_NAME");

                var secondName =
                    GetValue(item, "SECOND_NAME");

                var thirdName =
                    GetValue(item, "THIRD_NAME");

                var fourthName =
                    GetValue(item, "FOURTH_NAME");

                var fullname = string.Join("", new[]
                { firstName, secondName, thirdName, fourthName }.
                Where(x => !string.IsNullOrWhiteSpace(x) && !string.Equals(x, "n", StringComparison.OrdinalIgnoreCase)));
                var referenceNo = GetValue(item, "REFERENCE_NUMBER");
                var nationality =
                  item.Element("NATIONALITY")
                      ?.Element("VALUE")
                      ?.Value;

                var remarks =
                    GetValue(item, "COMMENTS1");

                var dob =
                    GetFirstDateOfBirth(item);
                if (string.IsNullOrWhiteSpace(fullname) || string.IsNullOrWhiteSpace(referenceNo))
                { continue; }
                records.Add(new AmlRecord
                {
                    RecordType= "Individual",
                    FullName = fullname,
                    ReferenceNo=referenceNo,
                    Nationality=CleanValue(nationality),
                    Remarks=CleanValue(remarks),
                    DateOfBirth=dob,
                    SourceName="UN Sanctions"
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
        private static string? CleanValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (value.Equals(
                "na",
                StringComparison.OrdinalIgnoreCase))
            {
                return null;
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
