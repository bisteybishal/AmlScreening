using AmlScreening.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Application.Interface.Provider
{
    public interface IAmlDataProvider
    {
        string sourcecode { get; }
        Task<List<AmlRecord>> GetRecordsAsync();
    }
}
