using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Application.Interface.Security
{
    public interface IPasswordService
    {
        bool Verify(
            string hashedPassword,
            string enteredPassword);
    }
}