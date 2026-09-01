using AmlScreening.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Application.Interface.Repositories
{
    public interface IAmlImportRepository
    {
        Task SaveAsync(IEnumerable<AmlRecord> records);
    }
}
