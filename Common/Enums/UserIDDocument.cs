using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums
{
    public enum UserIDDocumentType
    {
        passport, national_id_card, drivers_license, other_government_id, selfie_with_id
    }
    public enum SideDocument
    {
        front, back, bio_page, other
    }
    public enum DocumentStatus
    {
        pending, verified, rejected, expired
    }
}
