using AmlScreening.Application.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Application.Interface.Repositories
{
    public interface IAuthenticationRepository
    {
        Task<loginuserData?> GetByUsernameAsync(
            string username);

        Task RecordFailedLoginAsync(
            long userId);
    }
}
