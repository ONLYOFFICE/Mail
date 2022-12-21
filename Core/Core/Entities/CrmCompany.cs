using ASC.Common.Security;
using Nest;

namespace ASC.Mail.Core.Core.Entities
{
    [Serializable]
    public class CrmCompany : ASC.Core.Common.EF.Model.CrmContact, ISecurityObjectId
    {
        public CrmCompany()
        {
            CompanyName = string.Empty;
        }

        public Type ObjectType => GetType();

        public object SecurityId => Id;
    }
}
