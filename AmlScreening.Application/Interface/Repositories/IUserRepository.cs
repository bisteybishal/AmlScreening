using AmlScreening.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Application.Interface.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetUserAsync(string username);
    }
}
