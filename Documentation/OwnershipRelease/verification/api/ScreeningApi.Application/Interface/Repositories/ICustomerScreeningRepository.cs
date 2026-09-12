using ScreeningApi.Application.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScreeningApi.Application.Interface.Repositories
{
    public interface ICustomerScreeningRepository
    {
        Task<CustomerScreeningDbResult>ScreenAsync(string fullname,CancellationToken cancellationToken);
    }
}
