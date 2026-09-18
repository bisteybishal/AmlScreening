using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Domain.Common
{
    public interface IPartnerOwnedEntity
    {
        int PartnerId { get; set; }
    }
}
