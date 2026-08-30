using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Application.Interface
{
    public interface IPasswordService
    {
        string Hash(string password);

        bool Verify(
            string hashedPassword,
            string enteredPassword);
    }
}
