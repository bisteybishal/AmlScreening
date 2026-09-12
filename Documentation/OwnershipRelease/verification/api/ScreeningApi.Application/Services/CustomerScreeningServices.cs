using ScreeningApi.Application.Interface.Repositories;
using ScreeningApi.Application.Interface.Services;
using ScreeningApi.Application.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScreeningApi.Application.Services
{
    public class CustomerScreeningServices:ICustomerScreeningServices
    {
        private readonly ICustomerScreeningRepository _customerScreeningRepository;

        public CustomerScreeningServices(ICustomerScreeningRepository customerScreeningRepository)
        {
            _customerScreeningRepository = customerScreeningRepository;
        }

        public async Task<CustomerScreeningResponse> ScreenAsync(CustomerScreeningRequest request, CancellationToken cancellationToken = default)
        {
            var result = await _customerScreeningRepository.ScreenAsync(request.Customer.FullName, cancellationToken);
            var hasmatch = result.IsUnMatch || result.IsEuMatch || result.IsPep;
            var reasons = new List<string>();
            if (result.IsUnMatch)
            {
                reasons.Add("Potential UN exact-name match found.");
            }
            if (result.IsEuMatch)
            {
                reasons.Add("Potential EU exact-name match found.");
            }
            if (result.IsPep)
            {
                reasons.Add("Potential PEP exact-name match found.");
            }

            if (!hasmatch)
                reasons.Add("No exact-name match found.");
            return new CustomerScreeningResponse
            {
                ClientReference = request.ClientReference,
                ScreeningStatus = hasmatch  ? "POTENTIAL_MATCH" : "CLEAR",
                IsUnMatch = result.IsUnMatch,
                IsEuMatch = result.IsEuMatch,
                IsPep = result.IsPep,
                HighestMatchScore = result.HighestMatchScore,              
                Decision = hasmatch ? "Review" : "Approve",
                Reasons = reasons,
                CorrelationId =Guid.NewGuid().ToString("N")
            };
        }
    }
}
